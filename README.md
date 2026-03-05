# Dedicated Google Chat browser

An unofficial desktop app for [Google Chat](http://chat.google.com) built with C#.net.

## Announcement

This app is maintained for our personal use.  
You may use this app as you wish, but we do not promise ongoing maintenance.  
We will not accept PRs for language translation as we cannot verify it.

### What's new in v3.1.1

* Prevent missing DMs (Beta)

### Motivation

* Google has [shutdown](https://support.google.com/chat/answer/10194711) the official Google Chat Desktop App in March
  2021
* Google Chat Electron was [abandoned](https://github.com/ankurk91/google-chat-electron) by the original developer, [ankurk91](https://github.com/ankurk91), in Nov 2022.
    - His company moved from Google Chat to Slack and he himself stopped using it, so it's not an option.
    - We used to maintain and use this by updating the library. However, it no longer can be maintained because it contains code that is not ESM compliant. So we rewrote it as a WPF application in C#.net.
* Google is forcing users to use PWA which has less features
* You don't want to install Chrome; just to use a PWA. :wink:

### Installation (Windows)

* You can download the latest application from
  [releases](https://github.com/khiyowa/google-chat-desktop/releases/latest) section

### Supported Platforms

The app should work on windows x64 and arm64 platforms, but due to lack of time; we test on most popular only.  

| OS/Platform         |    Version    |
|:--------------------|:-------------:|
| Windows             |       11      |

### Major features

* System tray
    - Unread message indicator
    - Close the app to tray when you close the app window
* Desktop notifications
    - Clicking on notification bring the app to focus and open the specific person chat/room
* System tray
    - Offline indicator (no internet or not logged-in)
* Open external links in your OS default web browser
* Preserve window position and size
    - Limitation: If the window is maximized, it will not be remembered correctly.
* Prevent missing Direct Messages (Beta)
    - Receiving a direct message, it will continue to display a notification and alert you with a dedicated melody.
    - You can change the melody by putting any dm.mp3 or dm.wav under resources/audio.
    - It is disabled by default and can be enabled by going to Options -> Prevent missing DMs in the menu bar.
    - Limitation: If the recipient's name ends with parentheses `e.g., Hiyowa Kyobashi (KHiyowa)` , the function does not work. Operation has not been confirmed in environments other than Japanese. In particular, unexpected behavior may occur with RTL languages.
* Prevent multiple chat app instances from running

Not yet implemented in v3.1.0
* Unread message counter in dock
* Auto check for internet on startup and keep retrying to connect every 60 seconds if offline
* CTRL+F shortcut to search

### Auto Start

We currently have no plans to implement an auto-start feature within the app.
If you want the app to start automatically when you log in, please create a shortcut to the application and place it in your Windows Startup folder (`shell:startup`).

### Acknowledgements
This app is based on following:
[Original](https://github.com/ankurk91/google-chat-electron/)
The logic and icons of the program are basically used as is.

## Disclaimer

This desktop app is just a wrapper which starts a chromium instance locally and runs the actual web-app in it. All
rights to the [Google Chat](https://chat.google.com/) product is reserved by
[Google Inc.](https://en.wikipedia.org/wiki/Google)
This desktop client has no way to access none of your data.

## License

[GNU GPLv3](LICENSE) License
