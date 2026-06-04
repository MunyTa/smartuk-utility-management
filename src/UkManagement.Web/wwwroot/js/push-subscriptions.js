(() => {
  const button = document.getElementById("push-subscribe-button");
  const status = document.getElementById("push-subscribe-status");

  if (!button || !status) {
    return;
  }

  const setStatus = (message, isError = false) => {
    status.textContent = message;
    status.classList.toggle("text-danger", isError);
    status.classList.toggle("text-muted", !isError);
  };

  const toUint8Array = (base64Url) => {
    const padding = "=".repeat((4 - base64Url.length % 4) % 4);
    const base64 = (base64Url + padding).replace(/-/g, "+").replace(/_/g, "/");
    const rawData = window.atob(base64);
    const outputArray = new Uint8Array(rawData.length);

    for (let i = 0; i < rawData.length; i += 1) {
      outputArray[i] = rawData.charCodeAt(i);
    }

    return outputArray;
  };

  button.addEventListener("click", async () => {
    button.disabled = true;
    setStatus("Подключение...");

    try {
      if (!("serviceWorker" in navigator) || !("PushManager" in window) || !("Notification" in window)) {
        throw new Error("Этот браузер не поддерживает Web Push.");
      }

      const permission = await Notification.requestPermission();
      if (permission !== "granted") {
        throw new Error("Разрешение на уведомления не выдано.");
      }

      const keyResponse = await fetch("/api/push/vapid-public-key");
      if (!keyResponse.ok) {
        throw new Error("VAPID-ключи не настроены на сервере.");
      }

      const { publicKey } = await keyResponse.json();
      const registration = await navigator.serviceWorker.register("/push-sw.js");
      const readyRegistration = await navigator.serviceWorker.ready;

      let subscription = await readyRegistration.pushManager.getSubscription();
      if (!subscription) {
        subscription = await readyRegistration.pushManager.subscribe({
          userVisibleOnly: true,
          applicationServerKey: toUint8Array(publicKey)
        });
      }

      const payload = subscription.toJSON();
      const saveResponse = await fetch("/api/push/subscriptions", {
        method: "POST",
        headers: {
          "Content-Type": "application/json"
        },
        body: JSON.stringify({
          endpoint: payload.endpoint,
          keys: payload.keys,
          userAgent: navigator.userAgent
        })
      });

      if (!saveResponse.ok) {
        throw new Error("Не удалось сохранить push-подписку.");
      }

      setStatus("Браузерные уведомления подключены");
    } catch (error) {
      setStatus(error.message || "Не удалось подключить push.", true);
    } finally {
      button.disabled = false;
    }
  });
})();
