//! Sideload helper for the MacLess Builder Unity tool, built on isideload.
//!
//! Protocol: one JSON object per line on stdout (`{"event": ...}`); logs go to stderr.
//! The Apple ID password is the first line on stdin and 2FA answers follow it, so secrets never
//! appear in the process arguments, which other processes can read.

use std::collections::{HashMap, HashSet};
use std::io::{BufRead, Write};
use std::path::{Path, PathBuf};
use std::sync::atomic::{AtomicU64, Ordering};

use idevice::IdeviceService;
use idevice::lockdown::LockdownClient;
use idevice::provider::{IdeviceProvider, UsbmuxdProvider};
use idevice::usbmuxd::{UsbmuxdAddr, UsbmuxdConnection};
use isideload::anisette::remote_v3::RemoteV3AnisetteProvider;
use isideload::auth::apple_account::{AppleAccount, TwoFactorCallbackParams, TwoFactorCallbackResponse};
use isideload::dev::certificates::DevelopmentCertificate;
use isideload::dev::developer_session::DeveloperSession;
use isideload::dev::devices::DevicesApi;
use isideload::sideload::builder::MaxCertsBehavior;
use isideload::sideload::{SideloaderBuilder, TeamSelection};
use isideload::util::device::IdeviceInfo;
use isideload::util::keyring_storage::KeyringStorage;
use rootcause::prelude::*;
use serde_json::{Value, json};

// These three keep the tool's first name: Apple and Credential Manager already know them, and a new machine
// name or storage service would make isideload request another development certificate.
const MACHINE_NAME: &str = "iOS Cloud Build";
const PAIRING_LABEL: &str = "ios-cloud-build";
// Windows Credential Manager service that holds the anisette identity and signing certificates.
const STORAGE_SERVICE: &str = "HorrorTools.IosCloudBuild";

// Only Revoke/Error are used, but MaxCertsBehavior still needs a concrete callback type.
type NoCertPrompt = fn(Vec<DevelopmentCertificate>) -> std::future::Ready<Result<Option<Vec<String>>, Report>>;

#[tokio::main]
async fn main() {
    rustls::crypto::ring::default_provider()
        .install_default()
        .expect("Failed to install the rustls crypto provider");
    isideload::init().expect("Failed to initialize error reporting");
    tracing_subscriber::fmt()
        .with_max_level(tracing_subscriber::filter::LevelFilter::WARN)
        .with_writer(std::io::stderr)
        .init();

    let args: Vec<String> = std::env::args().skip(1).collect();
    let options = parse_options(args.get(1..).unwrap_or_default());
    let result = match args.first().map(String::as_str).unwrap_or_default() {
        "version" => {
            emit(json!({ "event": "version", "version": env!("CARGO_PKG_VERSION") }));
            Ok(())
        }
        "devices" => list_devices().await,
        "login" => login(&options).await,
        "install" => install(&options).await,
        other => Err(report!("Unknown command '{other}'. Use version, devices, login or install.")),
    };

    if let Err(error) = result {
        emit(json!({ "event": "error", "message": plain(&format!("{error}")) }));
        std::process::exit(1);
    }
}

async fn connect_usbmuxd() -> Result<UsbmuxdConnection, Report> {
    Ok(UsbmuxdConnection::default()
        .await
        .context("Apple Mobile Device Service is not running. Install iTunes or Apple Devices.")?)
}

async fn describe(provider: &UsbmuxdProvider) -> Result<(String, String, Option<bool>), Report> {
    let mut lockdown = LockdownClient::connect(provider).await.context("Failed to connect to the device")?;
    let pairing = provider
        .get_pairing_file()
        .await
        .context("The device has not trusted this PC. Unlock it and tap Trust.")?;
    lockdown.start_session(&pairing).await.context("Failed to start a device session")?;
    let name = lockdown.get_value(Some("DeviceName"), None).await.context("Failed to read the device name")?;
    let version = lockdown.get_value(Some("ProductVersion"), None).await.context("Failed to read the iOS version")?;
    // Missing before iOS 16, where Developer Mode does not exist.
    let dev_mode = lockdown
        .get_value(Some("DeveloperModeStatus"), Some("com.apple.security.mac.amfi"))
        .await
        .ok()
        .and_then(|value| value.as_boolean());
    Ok((
        name.as_string().unwrap_or_default().to_string(),
        version.as_string().unwrap_or_default().to_string(),
        dev_mode,
    ))
}

fn emit(value: Value) {
    let mut out = std::io::stdout().lock();
    let _ = writeln!(out, "{value}");
    let _ = out.flush();
}

async fn find_device(udid: &str) -> Result<UsbmuxdProvider, Report> {
    let mut usbmuxd = connect_usbmuxd().await?;
    let device = usbmuxd
        .get_devices()
        .await
        .context("Failed to list connected devices")?
        .into_iter()
        .find(|device| device.udid == udid)
        .ok_or_else(|| report!("The iPhone is not connected. Plug it in, unlock it and tap Trust."))?;
    let address = UsbmuxdAddr::from_env_var().context("Failed to resolve the usbmuxd address")?;
    Ok(device.to_provider(address, PAIRING_LABEL))
}

async fn install(options: &HashMap<String, String>) -> Result<(), Report> {
    let email = required(options, "email")?;
    let ipa = PathBuf::from(required(options, "ipa")?);
    let udid = required(options, "udid")?;

    progress("device", None);
    let provider = find_device(udid).await?;
    let device = IdeviceInfo::from_device(&provider).await.context("Failed to read the device")?;

    let mut account = sign_in(email, options).await?;
    let session = DeveloperSession::from_account(&mut account)
        .await
        .context("Failed to open an Apple developer session")?;
    // Revoking another machine's certificate breaks the apps it signed, so that needs an explicit opt-in.
    let max_certs: MaxCertsBehavior<NoCertPrompt> = if options.contains_key("revoke-certs") {
        MaxCertsBehavior::Revoke
    } else {
        MaxCertsBehavior::Error
    };
    let mut sideloader = SideloaderBuilder::new(session, email.to_string())
        .team_selection(TeamSelection::First)
        .max_certs_behavior(max_certs)
        .storage(Box::new(KeyringStorage::new(STORAGE_SERVICE.to_string())))
        .machine_name(MACHINE_NAME.to_string())
        .build();

    progress("register", None);
    let team = sideloader.get_team().await.context("Failed to read the Apple developer team")?;
    sideloader
        .get_dev_session()
        .ensure_device_registered(&team, &device.name, &device.udid, None)
        .await
        .context("Failed to register the device with Apple")?;

    progress("sign", Some(0.0));
    let (signed_app, _) = sideloader
        .sign_app(ipa.clone(), Some(team), true, Some(|fraction: f32| async move { progress("sign", Some(fraction)) }))
        .await
        .context("Failed to sign the app")?;

    // isideload reports 0-70 for the file transfer and 70-100 for the on-device install.
    let last_percent = AtomicU64::new(u64::MAX);
    let transfer = isideload::sideload::install::install_app(&provider, &signed_app, |percent| {
        if last_percent.swap(percent, Ordering::Relaxed) != percent {
            progress(if percent < 70 { "transfer" } else { "install" }, Some(percent as f32 / 100.0));
        }
    })
    .await;
    remove_extracted(&ipa);
    transfer.context("Failed to install the app on the device")?;

    emit(json!({ "event": "done", "device": device.name }));
    Ok(())
}

async fn list_devices() -> Result<(), Report> {
    let mut usbmuxd = connect_usbmuxd().await?;
    let address = UsbmuxdAddr::from_env_var().context("Failed to resolve the usbmuxd address")?;
    let mut seen = HashSet::new();
    // A phone on both USB and Wi-Fi is listed twice.
    for device in usbmuxd.get_devices().await.context("Failed to list connected devices")? {
        if !seen.insert(device.udid.clone()) {
            continue;
        }

        let provider = device.to_provider(address.clone(), PAIRING_LABEL);
        match describe(&provider).await {
            Ok((name, ios, dev_mode)) => emit(json!({
                "event": "device", "udid": device.udid, "name": name, "ios": ios,
                // Unity's JsonUtility has no nullable bool, so this is "on", "off" or "unknown" (before iOS 16).
                "devMode": match dev_mode { Some(true) => "on", Some(false) => "off", None => "unknown" },
            })),
            Err(error) => emit(json!({ "event": "device", "udid": device.udid, "error": plain(&format!("{error}")) })),
        }
    }

    emit(json!({ "event": "done" }));
    Ok(())
}

async fn login(options: &HashMap<String, String>) -> Result<(), Report> {
    sign_in(required(options, "email")?, options).await?;
    emit(json!({ "event": "done" }));
    Ok(())
}

fn parse_options(args: &[String]) -> HashMap<String, String> {
    let mut options = HashMap::new();
    let mut index = 0;
    while index < args.len() {
        if let Some(key) = args[index].strip_prefix("--") {
            // A key followed by another --key (or nothing) is a flag.
            match args.get(index + 1).filter(|value| !value.starts_with("--")) {
                Some(value) => {
                    options.insert(key.to_string(), value.clone());
                    index += 1;
                }
                None => {
                    options.insert(key.to_string(), String::from("true"));
                }
            }
        }

        index += 1;
    }

    options
}

// rootcause draws a tree with source locations; the Unity tool wants "outer: inner: cause".
fn plain(report: &str) -> String {
    report
        .lines()
        .map(|line| line.trim_start_matches([' ', '●', '├', '│', '╰', '─']).trim())
        .filter(|line| !line.is_empty() && !line.contains(".rs:"))
        .collect::<Vec<_>>()
        .join(": ")
}

fn progress(stage: &str, fraction: Option<f32>) {
    emit(json!({ "event": "progress", "stage": stage, "percent": fraction.map_or(-1, |f| (f * 100.0).round() as i32) }));
}

fn read_line() -> String {
    let mut line = String::new();
    let _ = std::io::stdin().lock().read_line(&mut line);
    line.trim_end_matches(['\r', '\n']).to_string()
}

fn remove_extracted(ipa: &Path) {
    // Application::new unpacks the IPA to %TEMP%\<name>_extracted and leaves it there.
    if let Some(name) = ipa.file_name() {
        let _ = std::fs::remove_dir_all(std::env::temp_dir().join(format!("{}_extracted", name.to_string_lossy())));
    }
}

fn required<'a>(options: &'a HashMap<String, String>, key: &str) -> Result<&'a str, Report> {
    options
        .get(key)
        .map(String::as_str)
        .ok_or_else(|| report!("Missing --{key}."))
}

async fn sign_in(email: &str, options: &HashMap<String, String>) -> Result<AppleAccount, Report> {
    let password = read_line();
    if password.is_empty() {
        bail!("No Apple ID password was given.");
    }

    let mut anisette = RemoteV3AnisetteProvider::default()?
        .set_storage(Box::new(KeyringStorage::new(STORAGE_SERVICE.to_string())));
    if let Some(url) = options.get("anisette") {
        anisette = anisette.set_url(url);
    }

    progress("login", None);
    let account = AppleAccount::builder(email)
        .anisette_provider(anisette)
        .login(&password, two_factor)
        .await
        .context("Apple ID sign-in failed")?;
    emit(json!({ "event": "logged_in" }));
    Ok(account)
}

async fn two_factor(params: TwoFactorCallbackParams) -> Result<TwoFactorCallbackResponse, Report> {
    emit(json!({
        "event": "need_2fa", "sms": params.sms, "retry": params.unknown, "lastError": params.last_error,
    }));
    // The Unity tool answers with the code, or with "!devices", "!resend" or "!abort".
    Ok(match read_line().trim() {
        "!devices" => TwoFactorCallbackResponse::SendToDevices,
        "!resend" => TwoFactorCallbackResponse::ResendCode,
        "" | "!abort" => TwoFactorCallbackResponse::Abort,
        code => TwoFactorCallbackResponse::SubmitCode(code.to_string()),
    })
}
