---
id: host-email
title: Host email providers
sidebar_label: Host email
---

The production host (`AvantiPoint.Packages.Host`) sends transactional email through a single configured provider.

## Configuration

```json
{
  "EmailSettings": {
    "Provider": "Postmark",
    "FromAddress": "noreply@example.com",
    "FromName": "Package Feed",
    "Postmark": { "ServerToken": "your-token" }
  }
}
```

Set `EmailSettings__Provider` in Docker or environment variables. Use `None` to disable sending (log-only).

## Supported providers

| Provider | `Provider` value | Docker env example |
|----------|------------------|-------------------|
| **Postmark** (recommended) | `Postmark` | `EmailSettings__Postmark__ServerToken` |
| SendGrid | `SendGrid` | `EmailSettings__SendGrid__ApiKey` |
| SMTP | `Smtp` | `EmailSettings__Smtp__Host`, `__Port`, `__Username`, `__Password` |
| Amazon SES | `AmazonSes` | `EmailSettings__AmazonSes__Region`, optional keys |
| Azure Communication Services | `AzureCommunicationServices` | `EmailSettings__AzureCommunicationServices__ConnectionString`, `__SenderAddress` |
| Mailgun | `Mailgun` | `EmailSettings__Mailgun__ApiKey`, `__Domain` |
| Resend | `Resend` | `EmailSettings__Resend__ApiKey` |
| Disabled | `None` | omit or set provider to `None` |

## Docker examples

### Postmark

```yaml
environment:
  EmailSettings__Provider: Postmark
  EmailSettings__FromAddress: noreply@example.com
  EmailSettings__FromName: Package Feed
  EmailSettings__Postmark__ServerToken: ${POSTMARK_TOKEN}
```

### SendGrid

```yaml
environment:
  EmailSettings__Provider: SendGrid
  EmailSettings__SendGrid__ApiKey: ${SENDGRID_API_KEY}
```

### SMTP (MailKit)

```yaml
environment:
  EmailSettings__Provider: Smtp
  EmailSettings__Smtp__Host: smtp.example.com
  EmailSettings__Smtp__Port: "587"
  EmailSettings__Smtp__Username: user
  EmailSettings__Smtp__Password: ${SMTP_PASSWORD}
  EmailSettings__Smtp__EnableSsl: "true"
```

### Amazon SES

```yaml
environment:
  EmailSettings__Provider: AmazonSes
  EmailSettings__AmazonSes__Region: us-east-1
  EmailSettings__AmazonSes__AccessKey: ${AWS_ACCESS_KEY_ID}
  EmailSettings__AmazonSes__SecretKey: ${AWS_SECRET_ACCESS_KEY}
```

### Azure Communication Services

```yaml
environment:
  EmailSettings__Provider: AzureCommunicationServices
  EmailSettings__AzureCommunicationServices__ConnectionString: ${ACS_CONNECTION_STRING}
  EmailSettings__AzureCommunicationServices__SenderAddress: DoNotReply@example.com
```

### Mailgun

```yaml
environment:
  EmailSettings__Provider: Mailgun
  EmailSettings__Mailgun__ApiKey: ${MAILGUN_API_KEY}
  EmailSettings__Mailgun__Domain: mg.example.com
```

### Resend

```yaml
environment:
  EmailSettings__Provider: Resend
  EmailSettings__Resend__ApiKey: ${RESEND_API_KEY}
```

## Events

All providers send the same Handlebars HTML templates (embedded in `Host.Admin`):

- Token created, revoked, regenerated, expiring (7d/3d), expired
- Package and symbol published
- Token first use from a new IP address
- User welcome, revoked, restored
