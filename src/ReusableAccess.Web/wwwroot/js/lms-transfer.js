/* The File is streamed browser → Web BFF → API; it never enters the Blazor circuit. */
const lmsTransfers = new WeakMap();
window.raLmsTransfer = {
    info(input) {
        const file = input.files[0];
        return file ? { name: file.name, size: file.size } : null;
    },
    reset(input) { input.value = ''; },
    cancel(input) {
        const transfer = lmsTransfers.get(input);
        if (!transfer) return;
        transfer.controller.abort();
        transfer.xhr?.abort();
    },
    async send(input, meter, id) {
        const file = input.files[0];
        if (!file || !/^[a-f\d-]{36}$/i.test(id)) return { status: 0 };
        const transfer = { controller: new AbortController(), xhr: null };
        lmsTransfers.set(input, transfer);
        try {
            const anti = await fetch('/bff/antiforgery-token', { mode: 'same-origin', redirect: 'error', cache: 'no-store', signal: transfer.controller.signal });
            if (!anti.ok) return { status: anti.status };
            const { requestToken } = await anti.json();
            if (transfer.controller.signal.aborted || !requestToken) return { status: 0 };
            return await new Promise(resolve => {
                const xhr = new XMLHttpRequest();
                transfer.xhr = xhr;
                xhr.open('PUT', '/bff/lms/uploads/' + id);
                xhr.timeout = 45 * 60 * 1000;
                xhr.setRequestHeader('X-CSRF-TOKEN', requestToken);
                xhr.setRequestHeader('Content-Type', 'application/octet-stream');
                xhr.upload.onprogress = e => { if (meter) meter.value = e.lengthComputable ? 100 * e.loaded / e.total : 0; };
                xhr.onload = () => resolve({ status: xhr.status });
                xhr.onerror = xhr.onabort = xhr.ontimeout = () => resolve({ status: 0 });
                xhr.send(file);
            });
        } catch { return { status: 0 }; }
        finally { if (lmsTransfers.get(input) === transfer) lmsTransfers.delete(input); }
    }
};
