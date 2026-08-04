# PostyFox Connect spike

This spike uses one Manifest V3 extension codebase for Chrome, Edge, and Safari. It reads only the
FurAffinity `a` and `b` session cookies after the user logs in normally, then exchanges them through
a five-minute, one-use PostyFox pairing token. Cookie values are never displayed or stored by the
extension.

## Chrome / Edge

1. Open `chrome://extensions` or `edge://extensions`.
2. Enable developer mode and choose **Load unpacked**.
3. Select `clients/postyfox-connect/browser-extension`.
4. Log into `https://www.furaffinity.net/`.
5. In PostyFox, create the FurAffinity connector and call
   `POST /api/connectors/{connectorId}/cookie-pairing/start`.
6. Open the extension, enter the PostyFox origin and returned `pairingToken`, then connect.

## Safari on macOS, iPhone, and iPad
Due to being ENTIRELY uncertain if this will actually work, this needs tested before spending any more time on this route :/

Apple packages Safari Web Extensions inside a containing app. On a Mac with current Xcode:

```bash
bash clients/postyfox-connect/safari/generate-project.sh
open clients/postyfox-connect/safari/generated/PostyFox\ Connect/PostyFox\ Connect.xcodeproj
```

Select an Apple development team and unique bundle identifiers, then run the iOS target on an
iPhone/iPad or distribute it through TestFlight. The extension's **Check session** action reports
only whether cookie names were found and their HttpOnly flags; use this to establish whether the
target Safari/iPadOS version exposes FurAffinity's cookies before going any further.

The generated Xcode project is intentionally not committed. Regenerate it from the shared extension
source so Chrome/Edge/Safari behavior stays aligned.

## Security boundaries

- FurAffinity access is declared explicitly in `host_permissions`.
- Access to a user-entered PostyFox origin is requested interactively.
- Plain HTTP is rejected except for localhost development.
- Pairing tokens expire after five minutes, are single-use, and are persisted server-side only as a
  SHA-256 hash.
- The completion endpoint accepts only FurAffinity's required `a` and `b` cookie names.
- The public OIDC edge bypasses login only for the pairing completion route; its random one-use
  bearer token is the authorization for that call.

## Local validation

```bash
node clients/postyfox-connect/scripts/validate.mjs
node --check clients/postyfox-connect/browser-extension/popup.js
```
