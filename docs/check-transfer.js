/* Offline test of the browser transfer adapter. It does not test Blazor, SQL, HTTP or a WAF. */
const vm = require('node:vm');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const source = fs.readFileSync(path.join(__dirname, '../src/ReusableAccess.Web/wwwroot/js/lms-transfer.js'), 'utf8');
const id = 'aaaaaaaa-bbbb-4ccc-9ddd-eeeeeeeeeeee';
const file = { name: 'lecture.pdf', size: 100 };
const input = { files: [file] };
let antiStatus = 200, network = 204, transfers = 0, openTarget, payload;
const headers = {};
const fetch = async (url, init) => {
    assert.equal(url, '/bff/antiforgery-token');
    assert.equal(init.mode, 'same-origin');
    return { ok: antiStatus === 200, status: antiStatus, json: async () => ({ requestToken: 'fake-in-memory-antiforgery' }) };
};
class FakeXHR {
    upload = {};
    open(method, target) { assert.equal(method, 'PUT'); openTarget = target; transfers++; }
    setRequestHeader(k, v) { headers[k] = v; }
    send(body) {
        payload = body;
        this.upload.onprogress({ lengthComputable: true, loaded: 25, total: 100 });
        if (network === 0) return this.onerror();
        this.status = network;
        this.onload();
    }
}
const sandbox = { window: {}, fetch, XMLHttpRequest: FakeXHR };
vm.runInNewContext(source, sandbox);
const adapter = sandbox.window.raLmsTransfer;
(async () => {
    const progress = {};
    assert.equal(adapter.info(input).name, file.name);
    assert.equal(adapter.info(input).size, file.size);
    assert.equal((await adapter.send(input, progress, id)).status, 204);
    assert.equal(openTarget, '/bff/lms/uploads/' + id);
    assert.equal(payload, file); // same File, not a Base64 payload
    assert.equal(progress.value, 25);
    assert.equal(headers['Content-Type'], 'application/octet-stream');
    assert.equal(headers['X-CSRF-TOKEN'], 'fake-in-memory-antiforgery');
    antiStatus = 403;
    assert.equal((await adapter.send(input, progress, id)).status, 403);
    assert.equal(transfers, 1);
    antiStatus = 200; network = 0;
    assert.equal((await adapter.send(input, progress, id)).status, 0);
    network = 413;
    assert.equal((await adapter.send(input, progress, id)).status, 413);
    assert.equal((await adapter.send({ files: [] }, progress, id)).status, 0);
    assert.equal((await adapter.send(input, progress, 'wrong')).status, 0);
    console.log('PASS: transfer adapter (mocked browser/transport); no SDK or network used.');
})().catch(e => { console.error(e); process.exitCode = 1; });
