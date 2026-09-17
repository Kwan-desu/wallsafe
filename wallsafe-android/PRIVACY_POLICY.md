# Privacy Policy for WallSafe

**Last Updated:** September 14, 2026  
**Effective Date:** September 14, 2026  

WallSafe ("we", "our", or "the app") is a privacy-first anime wallpaper manager and viewer designed with discretion, local-first architecture, and utmost respect for user privacy. This Privacy Policy outlines how WallSafe operates, what information is accessed, and why.

---

## 1. Zero Personal Data Collection

**WallSafe does not collect, transmit, store, sell, or profile your personal data.**

Specifically:
- **No Account Required:** You do not need to register, log in, or provide any personal details (name, email, phone number) to use WallSafe.
- **No Tracking or Telemetry:** WallSafe contains **no** third-party analytics SDKs, advertising trackers, user behavior tracking, or crash analytics beacons.
- **No Profiling or Selling:** We never track your search history, preferences, or device characteristics, and we never sell or share data with third-party data brokers.

---

## 2. On-Device, Local-First Storage

All your activity in WallSafe is stored exclusively on your physical device:
- **Favorites & Collections:** Stored locally in a secure SQLite database on your device.
- **Downloaded Wallpapers:** Stored locally in your device's MediaStore or app-specific storage.
- **Settings & Preferences:** Stored locally via Android Jetpack DataStore.
- **Image Caches:** Cached in your local temporary storage to accelerate loading and can be cleared anytime via Settings.

---

## 3. Network Access & Third-Party APIs

To search and display anime wallpapers, WallSafe connects to public, community-maintained image APIs (such as Yande.re and Konachan):
- **API Queries:** When you browse, search, or refresh wallpapers, WallSafe transmits the search query (e.g., tags, page number, rating filter) directly to the selected public image service via secure HTTPS.
- **No Personal Identifiers Sent:** WallSafe only sends standard HTTP request headers required for network connectivity (such as a generic `User-Agent` string: `WallSafe/1.0 (Android)`). We do not send your device ID, advertising ID, or any identifying markers.
- **Third-Party Privacy:** Images and metadata are hosted on third-party servers. When accessing third-party endpoints, their respective privacy policies and terms may apply.

---

## 4. Android Device Permissions Explained

WallSafe strictly adheres to the principle of least privilege, requesting only the permissions necessary for its features:

| Permission | Purpose | Data Handling |
| :--- | :--- | :--- |
| **`INTERNET`** | Fetches wallpaper thumbnails and high-resolution artwork from public image services. | Transmits search queries over encrypted HTTPS; no personal data transmitted. |
| **`ACCESS_NETWORK_STATE`** | Verifies active internet connectivity before attempting network requests. | Evaluated locally on device; never transmitted. |
| **`SET_WALLPAPER`** | Applies your selected wallpaper directly to your Home Screen, Lock Screen, or both. | Handled entirely on-device via Android `WallpaperManager`. |
| **`VIBRATE`** | Provides discrete haptic feedback when favoriting or applying wallpapers. | Executed locally via Android `VibratorManager`. |
| **`POST_NOTIFICATIONS`** *(Optional)* | Displays download completion notifications on Android 13+. | Handled locally on-device. |
| **`WRITE_EXTERNAL_STORAGE`** *(Legacy, Android 9 and below)* | Allows saving downloaded wallpapers to device storage on older Android versions. | Used only to save image files chosen by the user. |

---

## 5. Discretion Features

WallSafe includes specialized privacy and discretion features:
- **Discretion Blur:** Automatically blurs sensitive or suggestive thumbnails until the user taps to temporarily reveal them.
- **On-Device Storage:** All favorites and downloaded media remain strictly private on the user's device.

All discretion features operate **strictly offline and locally** on your device.

---

## 6. Data Retention and Deletion

Because WallSafe does not maintain servers or cloud databases of user data:
- **Instant Deletion:** You can delete any saved favorite, history entry, or collection directly within the app.
- **Cache Clearing:** You can purge all cached images at any time in **Settings → Storage → Clear Cache**.
- **Complete Erasure:** Uninstalling WallSafe or clearing app data in **Android Settings → Apps → WallSafe → Clear Data** immediately and permanently deletes all preferences, favorites, and database entries from your device.

---

## 7. Children's Privacy (COPPA & GDPR Compliance)

WallSafe does not knowingly collect or solicit any personal data from children under the age of 13 (or under 16 in the European Union). The app defaults to Safe/SFW (Safe For Work) rating filters and includes customizable tag blacklists to exclude unsuitable content.

---

## 8. Security Safeguards

- **Transport Security:** All network communications with public image repositories are encrypted using Transport Layer Security (TLS/HTTPS).
- **Sandboxed Storage:** All database entries and settings are stored within Android's protected application sandbox, inaccessible to other standard applications without root permissions.

---

## 9. Changes to This Privacy Policy

We may update our Privacy Policy from time to time to reflect changes in functionality or legal requirements. Any modifications will be posted within the application settings and on our official repository with an updated effective date.

---

## 10. Contact Us

If you have any questions, feedback, or concerns regarding this Privacy Policy, please contact:

* **Project:** WallSafe (Android)
* **Email:** support@wallsafe.app
* **Repository:** https://github.com/Kwan-desu/wallsafe
