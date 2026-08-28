// =============================================================================
// Google Chat Desktop - Client Preload Script (No-BOM / LF)
// =============================================================================

(function () {
    if (window.__google_chat_desktop_installed__) {
        return;
    }
    window.__google_chat_desktop_installed__ = true;

    // Function to fetch and convert image to Base64 (Fetch with Canvas fallback)
    async function getBase64Image(url) {
        if (!url) return null;
        try {
            const response = await fetch(url, { mode: 'cors', credentials: 'omit' });
            const blob = await response.blob();
            const mimeType = blob.type || 'image/png';
            return new Promise((resolve, reject) => {
                const reader = new FileReader();
                reader.onloadend = () => resolve({ base64: reader.result.split(',')[1], mimeType });
                reader.onerror = reject;
                reader.readAsDataURL(blob);
            });
        } catch (error) {
            try {
                return await new Promise((resolve, reject) => {
                    const img = new Image();
                    img.crossOrigin = 'Anonymous';
                    img.onload = () => {
                        try {
                            const canvas = document.createElement('canvas');
                            canvas.width = img.naturalWidth || img.width || 128;
                            canvas.height = img.naturalHeight || img.height || 128;
                            const ctx = canvas.getContext('2d');
                            ctx.drawImage(img, 0, 0);
                            const dataUrl = canvas.toDataURL('image/png');
                            resolve({ base64: dataUrl.split(',')[1], mimeType: 'image/png' });
                        } catch (err) {
                            reject(err);
                        }
                    };
                    img.onerror = reject;
                    img.src = url;
                });
            } catch (e2) {
                console.error('Error fetching icon base64:', e2);
                return null;
            }
        }
    }

    function extractIconUrl(options) {
        if (!options) return null;
        if (options.icon && typeof options.icon === 'string') return options.icon;
        if (options.image && typeof options.image === 'string') return options.image;
        if (options.badge && typeof options.badge === 'string') return options.badge;
        if (options.data && typeof options.data === 'object') {
            if (options.data.icon) return options.data.icon;
            if (options.data.avatarUrl) return options.data.avatarUrl;
        }
        return null;
    }

    // TransparentNotification object (Fallback)
    class TransparentNotification extends EventTarget {
        constructor(title, options) {
            super();
            this.title = title;
            this.options = options;
        }

        click() {
            const event = new Event('click');
            this.dispatchEvent(event);
        }

        close() {
            const event = new Event('close');
            this.dispatchEvent(event);
        }
    }

    const notifications = new Map();
    const notificationDataMap = new Map();

    // 1. Intercept ServiceWorkerRegistration.prototype.showNotification (Google Chat main notification trigger)
    if (typeof ServiceWorkerRegistration !== 'undefined' && ServiceWorkerRegistration.prototype) {
        ServiceWorkerRegistration.prototype.showNotification = function (title, options = {}) {
            const iconUrl = extractIconUrl(options);
            const tag = options.tag || (options.data && (options.data.id || options.data.tag)) || String(Date.now());

            // Store notification data for click routing
            const notifPayload = (options.data !== undefined) ? options.data : options;
            notificationDataMap.set(tag, notifPayload);

            (async () => {
                let iconData = null;
                if (iconUrl) {
                    try {
                        iconData = await getBase64Image(iconUrl);
                    } catch (error) {
                        console.error('Error fetching icon:', error);
                    }
                }

                const message = {
                    type: 'notification',
                    title: title || 'Google Chat',
                    options: {
                        ...options,
                        body: options.body || '',
                        tag: tag,
                        iconBase64: iconData ? iconData.base64 : null,
                        iconMimeType: iconData ? iconData.mimeType : 'image/png'
                    }
                };

                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify(message));
                }
            })();

            // Suppress default WebView2 notification popup
            return Promise.resolve();
        };
    }

    // 2. Intercept window.Notification (Fallback)
    const OriginalNotification = window.Notification;
    window.Notification = function (title, options = {}) {
        const iconUrl = extractIconUrl(options);
        const tag = options.tag || String(Date.now());
        const notification = new TransparentNotification(title, options);
        notifications.set(tag, notification);
        notificationDataMap.set(tag, (options.data !== undefined) ? options.data : options);

        (async () => {
            let iconData = null;
            if (iconUrl) {
                try {
                    iconData = await getBase64Image(iconUrl);
                } catch (error) {
                    console.error('Error fetching icon:', error);
                }
            }

            const message = {
                type: 'notification',
                title: title || 'Google Chat',
                options: {
                    ...options,
                    body: options.body || '',
                    tag: tag,
                    iconBase64: iconData ? iconData.base64 : null,
                    iconMimeType: iconData ? iconData.mimeType : 'image/png'
                }
            };

            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify(message));
            }
        })();

        return notification;
    };

    window.Notification.permission = OriginalNotification ? OriginalNotification.permission : 'granted';
    if (OriginalNotification && OriginalNotification.requestPermission) {
        window.Notification.requestPermission = OriginalNotification.requestPermission.bind(OriginalNotification);
    }

    // Listen for notification click events from C#
    window.addEventListener('notificationClick', function (event) {
        const tag = event.detail.tag;

        // 1. Dispatch Service Worker message to Google Chat web client
        const notifData = notificationDataMap.get(tag);
        if (notifData) {
            const swClickMessage = {
                type: 'notificationClick',
                notification: notifData
            };

            // Dispatch to navigator.serviceWorker
            if (navigator.serviceWorker) {
                const msgEv = new MessageEvent('message', {
                    data: swClickMessage,
                    origin: window.location.origin
                });
                navigator.serviceWorker.dispatchEvent(msgEv);
                if (typeof navigator.serviceWorker.onmessage === 'function') {
                    try { navigator.serviceWorker.onmessage(msgEv); } catch (e) { }
                }
            }

            // Also post message to window (for any window-level listeners)
            window.postMessage(swClickMessage, '*');

            // Fallback: check for clickAction or url in notifData
            if (typeof notifData === 'object' && notifData !== null) {
                if (notifData.clickAction && typeof notifData.clickAction === 'string' && notifData.clickAction.startsWith('http')) {
                    window.location.href = notifData.clickAction;
                } else if (notifData.url && typeof notifData.url === 'string' && notifData.url.startsWith('http')) {
                    window.location.href = notifData.url;
                }
            }

            notificationDataMap.delete(tag);
        }

        // 2. Dispatch to legacy TransparentNotification object
        const legacyNotification = notifications.get(tag);
        if (legacyNotification) {
            legacyNotification.dispatchEvent(new Event('click'));
            notifications.delete(tag);
        }
    });

    // Listen for notification close events from C#
    window.addEventListener('notificationClose', function (event) {
        const tag = event.detail.tag;

        const notifData = notificationDataMap.get(tag);
        if (notifData) {
            const swCloseMessage = {
                type: 'notificationClose',
                notification: notifData
            };
            if (navigator.serviceWorker) {
                const msgEv = new MessageEvent('message', {
                    data: swCloseMessage,
                    origin: window.location.origin
                });
                navigator.serviceWorker.dispatchEvent(msgEv);
                if (typeof navigator.serviceWorker.onmessage === 'function') {
                    try { navigator.serviceWorker.onmessage(msgEv); } catch (e) { }
                }
            }
            notificationDataMap.delete(tag);
        }

        const legacyNotification = notifications.get(tag);
        if (legacyNotification) {
            legacyNotification.dispatchEvent(new Event('close'));
            notifications.delete(tag);
        }
    });

    // Function to get the current favicon URL
    function getFaviconUrl() {
        const link = document.querySelector("link[rel~='icon']");
        return link ? link.href : null;
    }

    // Function to evaluate favicon state
    function evaluateFaviconState(faviconUrl) {
        const fileName = faviconUrl.split('/').pop().toLowerCase();
        if (fileName.includes('chat') && fileName.includes('new') && fileName.includes('notif')) {
            return 'badge';
        } else if (fileName.includes('chat')) {
            return 'normal';
        } else {
            return 'offline';
        }
    }

    // Function to monitor favicon changes
    async function monitorFavicon() {
        let lastFaviconUrl = getFaviconUrl();

        if (lastFaviconUrl) {
            const initialFaviconState = evaluateFaviconState(lastFaviconUrl);
            const initialMessage = {
                type: 'favicon',
                state: initialFaviconState
            };
            if (window.chrome && window.chrome.webview) {
                window.chrome.webview.postMessage(JSON.stringify(initialMessage));
            }
        }

        setInterval(async () => {
            const currentFaviconUrl = getFaviconUrl();
            if (currentFaviconUrl && currentFaviconUrl !== lastFaviconUrl) {
                lastFaviconUrl = currentFaviconUrl;
                const faviconState = evaluateFaviconState(currentFaviconUrl);
                const message = {
                    type: 'favicon',
                    state: faviconState
                };
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify(message));
                }
            }
        }, 1000);
    }

    // Start monitoring favicon changes
    monitorFavicon();
})();
