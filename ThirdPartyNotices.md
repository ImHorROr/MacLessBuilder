# Third-party notices

The C# editor code in this package uses only Unity and .NET.

`Editor/Bin/sideload-helper.exe` is built from the source in `Helper~/` (MIT, see `Helper~/Cargo.toml`)
and statically links the Rust crates listed below. Key components:

- **isideload** by Nicholas Sharp (nab138), MIT: https://github.com/nab138/isideload
- **idevice** by Jackson Coxson, MIT: https://github.com/jkcoxson/idevice
- **apple-codesign-quick** by Dadoum, LGPL-2.1-or-later: https://github.com/Dadoum/apple-codesign-quick

## LGPL notice (apple-codesign-quick)

`sideload-helper.exe` statically links apple-codesign-quick, licensed under the GNU Lesser General
Public License 2.1 or later. To use a modified version of that library, change the dependency in
`Helper~/Cargo.toml` (for example with a `[patch.crates-io]` entry pointing at your copy) and rebuild:

    cd Helper~
    cargo build --release
    copy target\release\sideload-helper.exe ..\Editor\Bin\

The complete source of the helper and the exact dependency versions (`Helper~/Cargo.lock`) are in
the repository at https://github.com/ImHorROr/MacLessBuilder, and each release there offers the
source next to the download. The library's own source is at the URL above and on crates.io.

This tool is not affiliated with Apple, iloader or the projects above.

## All crates in sideload-helper.exe

Generated with `cargo tree -e normal --prefix none --format "{p}|{l}"`.

| Crate | License |
| --- | --- |
| adler2 v2.0.1 | 0BSD OR MIT OR Apache-2.0 |
| aead v0.5.2 | MIT OR Apache-2.0 |
| aead v0.6.1 | MIT OR Apache-2.0 |
| aes v0.8.4 | MIT OR Apache-2.0 |
| aes v0.9.3 | MIT OR Apache-2.0 |
| aes-gcm v0.11.1 | Apache-2.0 OR MIT |
| anyhow v1.0.104 | MIT OR Apache-2.0 |
| apple-codesign-quick v0.1.0 | LGPL-2.1-or-later |
| asn1-rs v0.7.2 | MIT OR Apache-2.0 |
| asn1-rs-derive v0.6.0 | MIT OR Apache-2.0 |
| asn1-rs-impl v0.2.0 | MIT/Apache-2.0 |
| async-compression v0.4.48 | MIT OR Apache-2.0 |
| async-stream v0.3.6 | MIT |
| async-stream-impl v0.3.6 | MIT |
| async-trait v0.1.92 | MIT OR Apache-2.0 |
| async_zip v0.0.18 | MIT |
| atomic-waker v1.1.2 | Apache-2.0 OR MIT |
| base64 v0.21.7 | MIT OR Apache-2.0 |
| base64 v0.22.1 | MIT OR Apache-2.0 |
| base64 v0.23.1 | MIT OR Apache-2.0 |
| base64ct v1.8.3 | Apache-2.0 OR MIT |
| bitflags v2.13.2 | MIT OR Apache-2.0 |
| block-buffer v0.10.4 | MIT OR Apache-2.0 |
| block-buffer v0.12.1 | MIT OR Apache-2.0 |
| block-padding v0.3.3 | MIT OR Apache-2.0 |
| block-padding v0.4.2 | MIT OR Apache-2.0 |
| bumpalo v3.20.3 | MIT OR Apache-2.0 |
| byteorder v1.5.0 | Unlicense OR MIT |
| bytes v1.12.1 | MIT |
| cbc v0.1.2 | MIT OR Apache-2.0 |
| cbc v0.2.1 | MIT OR Apache-2.0 |
| cfg-if v1.0.5 | MIT OR Apache-2.0 |
| chacha20 v0.10.2 | MIT OR Apache-2.0 |
| chacha20 v0.9.1 | Apache-2.0 OR MIT |
| chacha20poly1305 v0.10.1 | Apache-2.0 OR MIT |
| chrono v0.4.45 | MIT OR Apache-2.0 |
| cipher v0.4.4 | MIT OR Apache-2.0 |
| cipher v0.5.2 | MIT OR Apache-2.0 |
| cmov v0.5.4 | Apache-2.0 OR MIT |
| cms v0.2.3 | Apache-2.0 OR MIT |
| cms v0.3.0-pre.1 | Apache-2.0 OR MIT |
| compression-codecs v0.4.43 | MIT OR Apache-2.0 |
| compression-core v0.4.33 | MIT OR Apache-2.0 |
| const-oid v0.10.2 | Apache-2.0 OR MIT |
| const-oid v0.9.6 | Apache-2.0 OR MIT |
| core_detect v1.0.0 | MIT/Apache-2.0 |
| cpubits v0.1.1 | MIT OR Apache-2.0 |
| cpufeatures v0.2.17 | MIT OR Apache-2.0 |
| cpufeatures v0.3.1 | MIT OR Apache-2.0 |
| crc32fast v1.5.2 | MIT OR Apache-2.0 |
| crossbeam-deque v0.8.8 | MIT OR Apache-2.0 |
| crossbeam-epoch v0.9.21 | MIT OR Apache-2.0 |
| crossbeam-utils v0.8.23 | MIT OR Apache-2.0 |
| crypto-bigint v0.7.5 | Apache-2.0 OR MIT |
| crypto-common v0.1.7 | MIT OR Apache-2.0 |
| crypto-common v0.2.2 | MIT OR Apache-2.0 |
| ctr v0.10.1 | MIT OR Apache-2.0 |
| ctutils v0.4.2 | Apache-2.0 OR MIT |
| curve25519-dalek v4.1.3 | BSD-3-Clause |
| curve25519-dalek-derive v0.1.1 | MIT/Apache-2.0 |
| data-encoding v2.11.1 | MIT |
| der v0.7.10 | Apache-2.0 OR MIT |
| der v0.8.2 | Apache-2.0 OR MIT |
| der-parser v10.0.0 | MIT OR Apache-2.0 |
| der_derive v0.7.3 | Apache-2.0 OR MIT |
| der_derive v0.8.0 | Apache-2.0 OR MIT |
| deranged v0.5.8 | MIT OR Apache-2.0 |
| des v0.9.0 | MIT OR Apache-2.0 |
| digest v0.10.7 | MIT OR Apache-2.0 |
| digest v0.11.3 | MIT OR Apache-2.0 |
| displaydoc v0.2.7 | MIT OR Apache-2.0 |
| ed25519 v2.2.3 | Apache-2.0 OR MIT |
| ed25519-dalek v2.2.0 | BSD-3-Clause |
| either v1.18.0 | MIT OR Apache-2.0 |
| encoding_rs v0.8.42 | (Apache-2.0 OR MIT) AND BSD-3-Clause |
| equivalent v1.0.2 | Apache-2.0 OR MIT |
| fastrand v2.5.0 | Apache-2.0 OR MIT |
| flagset v0.4.7 | Apache-2.0 |
| flate2 v1.1.10 | MIT OR Apache-2.0 |
| fnv v1.0.7 | Apache-2.0 / MIT |
| form_urlencoded v1.2.2 | MIT OR Apache-2.0 |
| futures v0.3.34 | MIT OR Apache-2.0 |
| futures-channel v0.3.34 | MIT OR Apache-2.0 |
| futures-core v0.3.34 | MIT OR Apache-2.0 |
| futures-executor v0.3.34 | MIT OR Apache-2.0 |
| futures-io v0.3.34 | MIT OR Apache-2.0 |
| futures-lite v2.6.1 | Apache-2.0 OR MIT |
| futures-macro v0.3.34 | MIT OR Apache-2.0 |
| futures-sink v0.3.34 | MIT OR Apache-2.0 |
| futures-task v0.3.34 | MIT OR Apache-2.0 |
| futures-util v0.3.34 | MIT OR Apache-2.0 |
| generic-array v0.14.7 | MIT |
| getrandom v0.2.17 | MIT OR Apache-2.0 |
| getrandom v0.3.4 | MIT OR Apache-2.0 |
| getrandom v0.4.3 | MIT OR Apache-2.0 |
| ghash v0.6.0 | Apache-2.0 OR MIT |
| h2 v0.4.19 | MIT |
| hashbrown v0.16.1 | MIT OR Apache-2.0 |
| hashbrown v0.17.1 | MIT OR Apache-2.0 |
| hex v0.4.3 | MIT OR Apache-2.0 |
| hkdf v0.12.4 | MIT OR Apache-2.0 |
| hmac v0.12.1 | MIT OR Apache-2.0 |
| hmac v0.13.0 | MIT OR Apache-2.0 |
| http v1.5.0 | MIT OR Apache-2.0 |
| http-body v1.1.0 | MIT |
| http-body-util v0.1.5 | MIT |
| httparse v1.10.1 | MIT OR Apache-2.0 |
| hybrid-array v0.4.15 | MIT OR Apache-2.0 |
| hyper v1.11.1 | MIT |
| hyper-rustls v0.27.10 | Apache-2.0 OR ISC OR MIT |
| hyper-tls v0.6.0 | MIT/Apache-2.0 |
| hyper-util v0.1.21 | MIT |
| icu_collections v2.3.0 | Unicode-3.0 |
| icu_locale_core v2.3.0 | Unicode-3.0 |
| icu_normalizer v2.3.0 | Unicode-3.0 |
| icu_normalizer_data v2.3.0 | Unicode-3.0 |
| icu_properties v2.3.0 | Unicode-3.0 |
| icu_properties_data v2.3.0 | Unicode-3.0 |
| icu_provider v2.3.1 | Unicode-3.0 |
| idevice v0.1.68 | MIT |
| idevice-srp v0.6.0 | MIT OR Apache-2.0 |
| idna v1.1.0 | MIT OR Apache-2.0 |
| idna_adapter v1.2.2 | Apache-2.0 OR MIT |
| indexmap v2.14.2 | Apache-2.0 OR MIT |
| inout v0.1.4 | MIT OR Apache-2.0 |
| inout v0.2.2 | MIT OR Apache-2.0 |
| ipnet v2.12.2 | MIT OR Apache-2.0 |
| isideload v0.4.0 (https://github.com/nab138/isideload?rev=52b504c2cd706a9e415109b0be9e137168034f5e#52b504c2) | MIT |
| isideload-p12-keystore v0.3.0-rc4 | MIT/Apache-2.0 |
| isideload-vfs v0.0.1 | MIT OR Apache-2.0 |
| itoa v1.0.18 | MIT OR Apache-2.0 |
| keccak v0.1.6 | Apache-2.0 OR MIT |
| keyring v3.6.3 | MIT OR Apache-2.0 |
| lazy_static v1.5.0 | MIT OR Apache-2.0 |
| libc v0.2.189 | MIT OR Apache-2.0 |
| libm v0.2.16 | MIT |
| litemap v0.8.3 | Unicode-3.0 |
| log v0.4.34 | MIT OR Apache-2.0 |
| md-5 v0.10.6 | MIT OR Apache-2.0 |
| memchr v2.8.3 | Unlicense OR MIT |
| memmap2 v0.9.11 | MIT OR Apache-2.0 |
| mime v0.3.17 | MIT OR Apache-2.0 |
| minimal-lexical v0.2.1 | MIT/Apache-2.0 |
| miniz_oxide v0.9.1 | MIT OR Zlib OR Apache-2.0 |
| mio v1.2.3 | MIT |
| multiversion_no_op v1.0.0 | Apache-2.0 OR MIT |
| native-tls v0.2.18 | MIT OR Apache-2.0 |
| nom v7.1.3 | MIT |
| nu-ansi-term v0.50.3 | MIT |
| num-bigint v0.4.8 | MIT OR Apache-2.0 |
| num-bigint-dig v0.8.6 | MIT/Apache-2.0 |
| num-conv v0.2.2 | MIT OR Apache-2.0 |
| num-integer v0.1.47 | MIT OR Apache-2.0 |
| num-iter v0.1.46 | MIT OR Apache-2.0 |
| num-traits v0.2.19 | MIT OR Apache-2.0 |
| oid-registry v0.8.1 | MIT OR Apache-2.0 |
| once_cell v1.21.4 | MIT OR Apache-2.0 |
| opaque-debug v0.3.1 | MIT OR Apache-2.0 |
| parking v2.2.1 | Apache-2.0 OR MIT |
| pbkdf2 v0.13.0 | MIT OR Apache-2.0 |
| pem v4.0.0 | MIT |
| pem-rfc7468 v0.7.0 | Apache-2.0 OR MIT |
| pem-rfc7468 v1.0.0 | Apache-2.0 OR MIT |
| percent-encoding v2.3.2 | MIT OR Apache-2.0 |
| pin-project v1.1.13 | Apache-2.0 OR MIT |
| pin-project-internal v1.1.13 | Apache-2.0 OR MIT |
| pin-project-lite v0.2.17 | Apache-2.0 OR MIT |
| pkcs1 v0.7.5 | Apache-2.0 OR MIT |
| pkcs12 v0.2.0-pre.0 | Apache-2.0 OR MIT |
| pkcs5 v0.8.1 | Apache-2.0 OR MIT |
| pkcs8 v0.10.2 | Apache-2.0 OR MIT |
| pkcs8 v0.11.0 | Apache-2.0 OR MIT |
| plist v1.10.1 | MIT |
| plist-macro v0.1.6 | MIT |
| poly1305 v0.8.0 | Apache-2.0 OR MIT |
| polyval v0.7.3 | Apache-2.0 OR MIT |
| potential_utf v0.1.6 | Unicode-3.0 |
| powerfmt v0.2.0 | MIT OR Apache-2.0 |
| ppv-lite86 v0.2.21 | MIT OR Apache-2.0 |
| proc-macro2 v1.0.107 | MIT OR Apache-2.0 |
| quick-xml v0.42.0 | MIT |
| quote v1.0.47 | MIT OR Apache-2.0 |
| rand v0.10.3 | MIT OR Apache-2.0 |
| rand v0.8.8 | MIT OR Apache-2.0 |
| rand v0.9.5 | MIT OR Apache-2.0 |
| rand_chacha v0.3.1 | MIT OR Apache-2.0 |
| rand_chacha v0.9.0 | MIT OR Apache-2.0 |
| rand_core v0.10.1 | MIT OR Apache-2.0 |
| rand_core v0.6.4 | MIT OR Apache-2.0 |
| rand_core v0.9.5 | MIT OR Apache-2.0 |
| rayon v1.12.0 | MIT OR Apache-2.0 |
| rayon-core v1.13.0 | MIT OR Apache-2.0 |
| rc2 v0.9.0 | MIT OR Apache-2.0 |
| rcgen v0.14.10 | MIT OR Apache-2.0 |
| reqwest v0.13.5 | MIT OR Apache-2.0 |
| reqwest-middleware v0.5.2 | MIT OR Apache-2.0 |
| ring v0.17.14 | Apache-2.0 AND ISC |
| rootcause v0.12.1 | MIT/Apache-2.0 |
| rootcause-internals v0.12.1 | MIT/Apache-2.0 |
| rsa v0.9.10 | MIT OR Apache-2.0 |
| rustc-hash v2.1.3 | Apache-2.0 OR MIT |
| rusticata-macros v4.1.0 | MIT/Apache-2.0 |
| rustls v0.23.45 | Apache-2.0 OR ISC OR MIT |
| rustls-pki-types v1.15.1 | MIT OR Apache-2.0 |
| rustls-platform-verifier v0.7.1 | MIT OR Apache-2.0 |
| rustls-webpki v0.103.15 | ISC |
| salsa20 v0.11.0 | MIT OR Apache-2.0 |
| schannel v0.1.29 | MIT |
| scopeguard v1.2.0 | MIT OR Apache-2.0 |
| scrypt v0.12.0 | MIT OR Apache-2.0 |
| serde v1.0.229 | MIT OR Apache-2.0 |
| serde_core v1.0.229 | MIT OR Apache-2.0 |
| serde_derive v1.0.229 | MIT OR Apache-2.0 |
| serde_json v1.0.151 | MIT OR Apache-2.0 |
| sha1 v0.10.7 | MIT OR Apache-2.0 |
| sha1 v0.11.0 | MIT OR Apache-2.0 |
| sha2 v0.10.9 | MIT OR Apache-2.0 |
| sha2 v0.11.0 | MIT OR Apache-2.0 |
| sha3 v0.10.9 | MIT OR Apache-2.0 |
| sharded-slab v0.1.7 | MIT |
| sideload-helper v0.1.0 (this package, Helper~) | MIT |
| signature v2.2.0 | Apache-2.0 OR MIT |
| simd-adler32 v0.3.10 | MIT |
| simdutf8 v0.1.5 | MIT OR Apache-2.0 |
| siphasher v1.0.4 | MIT OR Apache-2.0 |
| slab v0.4.12 | MIT |
| smallvec v1.16.2 | MIT OR Apache-2.0 |
| socket2 v0.6.5 | MIT OR Apache-2.0 |
| spin v0.9.9 | MIT |
| spki v0.7.3 | Apache-2.0 OR MIT |
| spki v0.8.0 | Apache-2.0 OR MIT |
| srp v0.7.0-rc.3 | MIT OR Apache-2.0 |
| stable_deref_trait v1.2.1 | MIT OR Apache-2.0 |
| subtle v2.6.1 | BSD-3-Clause |
| syn v2.0.119 | MIT OR Apache-2.0 |
| syn v3.0.6 | MIT OR Apache-2.0 |
| sync_wrapper v1.0.2 | Apache-2.0 |
| synstructure v0.13.2 | MIT |
| synstructure v0.14.0 | MIT |
| thiserror v2.0.21 | MIT OR Apache-2.0 |
| thiserror-impl v2.0.21 | MIT OR Apache-2.0 |
| thread_local v1.1.10 | MIT OR Apache-2.0 |
| time v0.3.55 | MIT OR Apache-2.0 |
| time-core v0.1.9 | MIT OR Apache-2.0 |
| time-macros v0.2.32 | MIT OR Apache-2.0 |
| tinystr v0.8.4 | Unicode-3.0 |
| tokio v1.53.1 | MIT |
| tokio-macros v2.7.2 | MIT |
| tokio-native-tls v0.3.1 | MIT |
| tokio-rustls v0.26.5 | MIT OR Apache-2.0 |
| tokio-tungstenite v0.29.0 | MIT |
| tokio-util v0.7.19 | MIT |
| tower v0.5.3 | MIT |
| tower-http v0.6.11 | MIT |
| tower-layer v0.3.3 | MIT |
| tower-service v0.3.3 | MIT |
| tracing v0.1.44 | MIT |
| tracing-attributes v0.1.31 | MIT |
| tracing-core v0.1.36 | MIT |
| tracing-log v0.2.0 | MIT |
| tracing-subscriber v0.3.23 | MIT |
| triomphe v0.1.16 | MIT OR Apache-2.0 |
| try-lock v0.2.5 | MIT |
| tungstenite v0.29.0 | MIT OR Apache-2.0 |
| typed-path v0.12.3 | MIT OR Apache-2.0 |
| typenum v1.20.1 | MIT OR Apache-2.0 |
| unicode-ident v1.0.26 | (MIT OR Apache-2.0) AND Unicode-3.0 |
| universal-hash v0.5.1 | MIT OR Apache-2.0 |
| universal-hash v0.6.1 | MIT OR Apache-2.0 |
| untrusted v0.9.0 | ISC |
| url v2.5.8 | MIT OR Apache-2.0 |
| urlencoding v2.1.3 | MIT |
| utf8_iter v1.0.4 | Apache-2.0 OR MIT |
| uuid v1.26.1 | Apache-2.0 OR MIT |
| want v0.3.1 | MIT |
| web-time v1.1.0 | MIT OR Apache-2.0 |
| webpki-roots v0.26.11 | CDLA-Permissive-2.0 |
| webpki-roots v1.0.9 | CDLA-Permissive-2.0 |
| windows-link v0.2.1 | MIT OR Apache-2.0 |
| windows-registry v0.6.1 | MIT OR Apache-2.0 |
| windows-result v0.4.1 | MIT OR Apache-2.0 |
| windows-strings v0.5.1 | MIT OR Apache-2.0 |
| windows-sys v0.60.2 | MIT OR Apache-2.0 |
| windows-sys v0.61.2 | MIT OR Apache-2.0 |
| windows-targets v0.53.5 | MIT OR Apache-2.0 |
| windows_x86_64_msvc v0.53.1 | MIT OR Apache-2.0 |
| writeable v0.6.4 | Unicode-3.0 |
| x25519-dalek v2.0.1 | BSD-3-Clause |
| x509-cert v0.2.5 | Apache-2.0 OR MIT |
| x509-cert v0.3.0 | Apache-2.0 OR MIT |
| x509-parser v0.18.1 | MIT OR Apache-2.0 |
| yasna v0.6.0 | MIT OR Apache-2.0 |
| yoke v0.8.3 | Unicode-3.0 |
| yoke-derive v0.8.3 | Unicode-3.0 |
| zerocopy v0.8.59 | BSD-2-Clause OR Apache-2.0 OR MIT |
| zerofrom v0.1.8 | Unicode-3.0 |
| zerofrom-derive v0.1.8 | Unicode-3.0 |
| zeroize v1.9.0 | Apache-2.0 OR MIT |
| zeroize_derive v1.5.0 | Apache-2.0 OR MIT |
| zerotrie v0.2.5 | Unicode-3.0 |
| zerovec v0.11.8 | Unicode-3.0 |
| zerovec-derive v0.11.6 | Unicode-3.0 |
| zip v8.6.0 | MIT |
| zlib-rs v0.6.8 | Zlib |
| zmij v1.0.23 | MIT |
| zopfli v0.8.3 | Apache-2.0 |
