# PostyFox Connect

One Manifest V3 extension codebase for Chrome, Edge, and Safari. It hands a website session that the
user is already logged into over to PostyFox, so connectors for sites with no API (FurAffinity) can
post on their behalf.

## How the one-click flow works

Opening the popup does the detection; the single button does the work.

1. `GET /api/connectors/cookie-pairing/targets` — sent with the user's PostyFox session cookie
   (Chrome treats extension-initiated requests as same-site, so it rides along). One call resolves
   which connector to update, which cookies to collect, and where the site's login page is.
2. The popup reads exactly those cookies for the site.
3. `POST /api/connectors/cookie-pairing/pair` sends them back. The server stores only the cookie
   names the platform declares, against the user's connector for that site — creating that connector
   if they have none yet.

There is nothing to type: the target is `https://cp.postyfox.com`, or `https://dev.postyfox.com` with
the **Dev** switch on. Both are declared in `host_permissions`, so switching never prompts.

If the user is not signed in to PostyFox, or not logged in to the website, the popup says which one
and its button opens the right page. Nothing else is asked of them.

Site knowledge lives on the server (`CookiePairingSpec` on the connector's descriptor), not in the
extension — a newly supported site needs no extension release, only a host permission if it is not
already covered.

### Pairing tokens (fallback)

`POST /api/connectors/{connectorId}/cookie-pairing/start` still mints a five-minute, one-use token,
and the popup's **Use a pairing token instead** section still redeems it through the anonymous
`POST /api/connectors/cookie-pairing/complete`. That route exists for a browser that cannot present a
PostyFox session — a different profile, or a Safari build where the session cookie does not reach the
extension. It reads site metadata from the anonymous `GET /api/connectors/cookie-pairing/sites`, so it
works while signed out.

## Chrome / Edge

1. Open `chrome://extensions` or `edge://extensions`.
2. Enable developer mode and choose **Load unpacked**.
3. Select `clients/postyfox-connect/browser-extension`.
4. Log into `https://www.furaffinity.net/` and sign in to PostyFox in the same browser.
5. Open the extension and click **Connect FurAffinity**.

## Safari on macOS, iPhone, and iPad
Due to being ENTIRELY uncertain if this will actually work, this needs tested before spending any more time on this route :/

Apple packages Safari Web Extensions inside a containing app. On a Mac with current Xcode:

```bash
bash clients/postyfox-connect/safari/generate-project.sh
open clients/postyfox-connect/safari/generated/PostyFox\ Connect/PostyFox\ Connect.xcodeproj
```

Select an Apple development team and unique bundle identifiers, then run the iOS target on an
iPhone/iPad or distribute it through TestFlight. Two things to establish there: whether the popup can
read FurAffinity's cookies at all, and whether the PostyFox session cookie reaches the extension. If
the first works but the second does not, the pairing-token fallback is the route.

The generated Xcode project is intentionally not committed. Regenerate it from the shared extension
source so Chrome/Edge/Safari behavior stays aligned.

## Security boundaries

- Site and PostyFox access are declared explicitly in `host_permissions`; a site PostyFox supports
  that is not covered statically is requested interactively, from the button press.
- Only the two published HTTPS origins are ever contacted — there is no user-supplied URL to redirect
  the cookies to.
- Only the cookie names the platform's descriptor declares are read, sent, or stored. Values are
  never displayed or persisted by the extension.
- `/cookie-pairing/pair` is authorized by the caller's own PostyFox session, so a browser that is not
  signed in cannot connect anything.
- `/cookie-pairing/sites` is anonymous but carries platform metadata only — no user context.
- Pairing tokens expire after five minutes, are single-use, and are persisted server-side only as a
  SHA-256 hash.
- The public OIDC edge bypasses login only for the pairing completion and site-metadata routes.

## Local validation

```bash
node clients/postyfox-connect/scripts/validate.mjs   # manifest + environment origins
node --check clients/postyfox-connect/browser-extension/popup.js
node clients/postyfox-connect/scripts/smoke.mjs      # popup states against a stubbed browser
```

`smoke.mjs` runs `popup.js` against a minimal DOM, a stubbed extension API, and a routed `fetch`, so
the connect / sign-in / log-in / permission states can be checked without loading Chrome.
