// Function to fetch and convert image to Base64
async function getBase64Image(url) {
    const response = await fetch(url);
    const blob = await response.blob();
    const mimeType = blob.type; // 画像のMIMEタイプを取得
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onloadend = () => resolve({ base64: reader.result.split(',')[1], mimeType });
        reader.onerror = reject;
        reader.readAsDataURL(blob);
    });
}

// TransparentNotification object
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

// Override the Notification object
const OriginalNotification = window.Notification;
const notifications = new Map();
window.Notification = function (title, options) {
    // Create a notification object but do not show it
    const notification = new TransparentNotification(title, options);
    if (options.tag) {
        notifications.set(options.tag, notification);
    }

    // Fetch icon data if available
    (async () => {
        let iconData = null;
        if (options.icon) {
            try {
                iconData = await getBase64Image(options.icon);
            } catch (error) {
                console.error('Error fetching icon:', error);
            }
        }

        const message = {
            type: 'notification',
            title,
            options: {
                ...options,
                iconBase64: iconData ? iconData.base64 : null,
                iconMimeType: iconData ? iconData.mimeType : null
            }
        };

        console.log('Notification:', JSON.stringify(message));
        window.chrome.webview.postMessage(JSON.stringify(message));
    })();

    return notification;
};

window.Notification.permission = OriginalNotification.permission;
window.Notification.requestPermission = OriginalNotification.requestPermission.bind(OriginalNotification);

// Listen for notification click events from C#
window.addEventListener('notificationClick', function (event) {
    const tag = event.detail.tag;
    const notification = notifications.get(tag);
    if (notification) {
        notification.dispatchEvent(new Event('click'));
        notifications.delete(tag);
    }
});
// Listen for notification close events from C#
window.addEventListener('notificationClose', function (event) {
    const tag = event.detail.tag;
    const notification = notifications.get(tag);
    if (notification) {
        notification.dispatchEvent(new Event('close'));
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

    // 初回実行時に現在のfaviconの状態を通知
    if (lastFaviconUrl) {
        const initialFaviconState = evaluateFaviconState(lastFaviconUrl);
        const initialMessage = {
            type: 'favicon',
            state: initialFaviconState
        };
        window.chrome.webview.postMessage(JSON.stringify(initialMessage));
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
            window.chrome.webview.postMessage(JSON.stringify(message));
        }
    }, 1000); // 1秒ごとにチェック
}

// Start monitoring favicon changes
monitorFavicon();