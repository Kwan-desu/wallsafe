# Google Play Console: Data Safety Form Reference Guide

When submitting WallSafe to the Google Play Console, use the following exact responses for the **Data Safety** questionnaire.

---

## 1. Data Collection and Sharing Overview

| Question | Answer | Rationale |
| :--- | :--- | :--- |
| **Does your app collect or share any of the required user data types?** | **No** | WallSafe does not collect, record, or transmit any user personal data, telemetry, or analytics. |
| **Is all of the user data collected by your app encrypted in transit?** | **Yes** | All network requests to image APIs utilize HTTPS (TLS 1.3 / 1.2). |
| **Do you provide a way for users to request that their data be deleted?** | **Yes** | All data (favorites, collections, settings, cache) is stored strictly on-device. Users can clear cache in app settings, or clear data / uninstall to immediately delete all information. |

---

## 2. Specific Data Types Declaration Table

| Category | Data Type | Collected? | Shared? | Purpose |
| :--- | :--- | :--- | :--- | :--- |
| **Location** | Approximate location | **No** | **No** | Not tracked. Not requested by the application. |
| | Precise location | **No** | **No** | Not tracked. Not requested by the application. |
| **Personal info** | Name, Email, User IDs, Address, Phone, Race, etc. | **No** | **No** | No user accounts or personal profiles exist. |
| **Financial info** | Credit card, bank account, purchase history | **No** | **No** | The app is free and does not process payments. |
| **Health and fitness**| Fitness data, medical data | **No** | **No** | Not applicable. |
| **Messages** | Emails, SMS, in-app messages | **No** | **No** | No messaging features. |
| **Photos and videos** | Photos or videos | **No** | **No** | Wallpapers are downloaded on-device; user gallery photos are never uploaded or read. |
| **Audio files** | Voice recordings, music | **No** | **No** | Not applicable. |
| **Files and docs** | Files, documents | **No** | **No** | Only app-downloaded wallpapers are managed. |
| **Calendar** | Calendar events | **No** | **No** | Not applicable. |
| **Contacts** | Contact list | **No** | **No** | Not applicable. |
| **App activity** | Page views, taps, interactions | **No** | **No** | No analytics or behavioral tracking SDKs. |
| **Web browsing** | Web browsing history | **No** | **No** | Not tracked. |
| **App info & performance** | Crash logs, diagnostics | **No** | **No** | No third-party crash reporter SDKs. |
| **Device or other IDs** | Device ID, IMEI, Advertising ID (AAID) | **No** | **No** | Google Play Advertising ID is not requested or utilized. |

---

## 3. Security Practices

- **Encryption in Transit:** Yes (All network traffic is encrypted via HTTPS).
- **Data Deletion Mechanism:** Users can delete all saved favorites, downloads, and cache directly within the app, or uninstall the app to immediately erase all local data.
- **Independent Security Review:** Not applicable (standard for self-contained open-source utilities).

---

## 4. Privacy Policy URL for Play Console Listing

Google Play requires a publicly accessible HTTPS link to your Privacy Policy.
You can host the included `docs/privacy_policy.html` on GitHub Pages, for example:
`https://kwan-desu.github.io/wallsafe/privacy_policy.html`
or in your repository:
`https://raw.githubusercontent.com/Kwan-desu/wallsafe/main/PRIVACY_POLICY.md`
