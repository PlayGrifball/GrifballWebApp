# Calling the REST API with a personal access token

Reads are already public (no sign-in needed: `GET /seasons`, say). A personal access token lets a script sign in
to write too, or read what signing in shows (a staff dashboard, your own profile), without handing it a
password. The same tokens sign MCP clients in (see [MCP server](mcp-server.md)); a token says which surface it
opens.

## Making one

Make a token on your profile page, under **Access tokens**. Choose:

- **Where**: the REST API, the MCP server, or both.
- **Read-only**, or not.

The site shows the token once; only its hash is kept. A token's scope can't be changed afterwards: make a new
one instead. Revoke a token on the same page; it stops working at once.

## Calling the API

Send it as a bearer token:

```sh
curl https://grifball.xyz/api/seasons/3 -H "Authorization: Bearer grif_..."
```

- It acts as the user who made it, with their roles as they are now (as MCP does).
- No antiforgery token is needed: that's only for a browser's cookie session.
- A token whose scope doesn't include the REST API (an MCP-only one) is refused with 401, saying so. So is a token
  that's unknown, revoked or expired, on every endpoint, including an anonymous read such as `GET /seasons`: a
  `grif_` token that was sent but didn't authenticate is never silently treated as no token at all.

## Read-only

A read-only token may call `GET` and `HEAD` only; any other method is refused with 403, saying the token is
read-only. On the MCP server, a read-only token's tools are its read-only ones: a tool that changes data
(the `ChangingData` classes, staff's actions) isn't listed or callable, and neither is one that queues a pull
from Halo (`QueuesHaloPull`): it saves games it finds, even though it doesn't change or delete anything itself.

## What a token can never do

Whatever its scope or read-only flag, a token may not:

- make, list or revoke tokens;
- change the account's email or linked logins;
- merge accounts, or undo a merge;
- delete the account.

These stay the browser's own. Calling one of these endpoints with a token is refused with 403. Resetting the
password is different: it's allowed with a token, because the one-time reset link's own token in the request body
is the real boundary there, not who (or what) is sending the request - denying it on top would protect nothing.

## Logs

A call signed in by a token is logged with the token's own ID and name, never its secret.

## The API's shape

Every endpoint, its request and response shapes, is at `/api/swagger` (Swagger UI; the document itself is
`/api/swagger/v1/swagger.json`). The same document is what the Angular client is generated from
(`grifballwebapp.client/openapi.json`).
