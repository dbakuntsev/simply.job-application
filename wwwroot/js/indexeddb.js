const DB_NAME = 'SimplyJobApplication';
const DB_VERSION = 3;

let _db = null;

async function openDb() {
    if (_db) return _db;
    return new Promise((resolve, reject) => {
        const req = indexedDB.open(DB_NAME, DB_VERSION);
        req.onupgradeneeded = e => {
            const db = e.target.result;

            // ── Version 1 stores ─────────────────────────────────────────────
            if (!db.objectStoreNames.contains('settings')) {
                db.createObjectStore('settings');
            }
            if (!db.objectStoreNames.contains('sessions')) {
                const s = db.createObjectStore('sessions', { keyPath: 'id' });
                s.createIndex('createdAt', 'createdAt');
            }
            if (!db.objectStoreNames.contains('files')) {
                const s = db.createObjectStore('files', { keyPath: 'id' });
                s.createIndex('lastUsedAt', 'lastUsedAt');
            }

            // ── Version 2 stores ─────────────────────────────────────────────
            if (e.oldVersion < 2) {
                const orgs = db.createObjectStore('organizations', { keyPath: 'id' });
                orgs.createIndex('name', 'name');

                const contacts = db.createObjectStore('contacts', { keyPath: 'id' });
                contacts.createIndex('organizationId', 'organizationId');
                contacts.createIndex('fullName', 'fullName');

                const roles = db.createObjectStore('contactOpportunityRoles', { keyPath: ['contactId', 'opportunityId'] });
                roles.createIndex('contactId', 'contactId');
                roles.createIndex('opportunityId', 'opportunityId');

                const opps = db.createObjectStore('opportunities', { keyPath: 'id' });
                opps.createIndex('organizationId', 'organizationId');
                opps.createIndex('createdAt', 'createdAt');

                const hist = db.createObjectStore('opportunityFieldHistory', { keyPath: 'id' });
                hist.createIndex('opportunityId', 'opportunityId');

                const corr = db.createObjectStore('correspondence', { keyPath: 'id' });
                corr.createIndex('opportunityId', 'opportunityId');

                const corrFiles = db.createObjectStore('correspondenceFiles', { keyPath: 'id' });
                corrFiles.createIndex('correspondenceId', 'correspondenceId');

                const resumes = db.createObjectStore('baseResumes', { keyPath: 'id' });
                resumes.createIndex('name', 'name');

                const resumeVers = db.createObjectStore('baseResumeVersions', { keyPath: 'id' });
                resumeVers.createIndex('resumeId', 'resumeId');

                db.createObjectStore('lookupIndustries', { keyPath: 'id' });
                db.createObjectStore('lookupContactRoles', { keyPath: 'id' });
            }

            // ── Version 3: postingUrl (string) → postingUrls (array) ─────────
            if (e.oldVersion < 3) {
                const store = e.target.transaction.objectStore('opportunities');
                const req = store.openCursor();
                req.onsuccess = ev => {
                    const cursor = ev.target.result;
                    if (!cursor) return;
                    const opp = cursor.value;
                    if (!Array.isArray(opp.postingUrls)) {
                        opp.postingUrls = (opp.postingUrl && opp.postingUrl.trim()) ? [opp.postingUrl] : [];
                        delete opp.postingUrl;
                        cursor.update(opp);
                    }
                    cursor.continue();
                };
            }
        };
        req.onsuccess = e => { _db = e.target.result; resolve(_db); };
        req.onerror = e => reject(e.target.error);
    });
}

function tx(store, mode, fn) {
    return openDb().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(store, mode);
        const r = fn(t.objectStore(store));
        r.onsuccess = () => resolve(r.result);
        r.onerror = () => reject(r.error);
    }));
}

function txMulti(stores, mode, fn) {
    return openDb().then(db => new Promise((resolve, reject) => {
        const t = db.transaction(stores, mode);
        t.onerror = () => reject(t.error);
        fn(t, resolve, reject);
    }));
}

// ── Settings ─────────────────────────────────────────────────────────────────

export async function getSetting(key) {
    const v = await tx('settings', 'readonly', s => s.get(key));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function setSetting(key, value) {
    return tx('settings', 'readwrite', s => s.put(value, key));
}

// ── Sessions ──────────────────────────────────────────────────────────────────

export async function getAllSessions() {
    const v = await tx('sessions', 'readonly', s => s.getAll());
    return JSON.stringify(v ?? []);
}

export async function getSession(id) {
    const v = await tx('sessions', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveSession(session) {
    return tx('sessions', 'readwrite', s => s.put(session));
}

export async function deleteSession(id) {
    return tx('sessions', 'readwrite', s => s.delete(id));
}

export async function clearSessions() {
    return tx('sessions', 'readwrite', s => s.clear());
}

// ── Files ─────────────────────────────────────────────────────────────────────

export async function getAllFiles() {
    const v = await tx('files', 'readonly', s => s.getAll());
    return JSON.stringify(v ?? []);
}

export async function getAllFileMeta() {
    const files = await tx('files', 'readonly', s => s.getAll());
    const meta = (files ?? []).map(f => ({ id: f.id, name: f.name, lastUsedAt: f.lastUsedAt, sessionCount: f.sessionCount }));
    return JSON.stringify(meta);
}

export async function getFile(id) {
    const v = await tx('files', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveFile(file) {
    return tx('files', 'readwrite', s => s.put(file));
}

export async function deleteFile(id) {
    return tx('files', 'readwrite', s => s.delete(id));
}

export async function clearFiles() {
    return tx('files', 'readwrite', s => s.clear());
}

export async function clearAllUserData() {
    const stores = [
        'sessions', 'files',
        'baseResumes', 'baseResumeVersions',
        'organizations', 'contacts', 'contactOpportunityRoles', 'lookupIndustries', 'lookupContactRoles',
        'opportunities', 'opportunityFieldHistory',
        'correspondence', 'correspondenceFiles',
    ];
    return txMulti(stores, 'readwrite', (t, resolve) => {
        for (const name of stores) t.objectStore(name).clear();
        t.oncomplete = () => resolve();
    });
}

// ── Organizations ─────────────────────────────────────────────────────────────

export async function getAllOrganizations() {
    const v = await tx('organizations', 'readonly', s => s.getAll());
    return JSON.stringify(v ?? []);
}

export async function getOrganization(id) {
    const v = await tx('organizations', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveOrganization(org) {
    return tx('organizations', 'readwrite', s => s.put(org));
}

export async function deleteOrganization(id) {
    return tx('organizations', 'readwrite', s => s.delete(id));
}

// ── Contacts ──────────────────────────────────────────────────────────────────

export async function getContactsByOrganization(orgId) {
    const v = await tx('contacts', 'readonly', s => s.index('organizationId').getAll(orgId));
    return JSON.stringify(v ?? []);
}

export async function getContact(id) {
    const v = await tx('contacts', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveContact(contact) {
    return tx('contacts', 'readwrite', s => s.put(contact));
}

export async function deleteContact(id) {
    return tx('contacts', 'readwrite', s => s.delete(id));
}

export async function getContactsCountPerOrganization() {
    const all = await tx('contacts', 'readonly', s => s.getAll());
    const map = {};
    for (const c of (all ?? [])) {
        if (c.organizationId) map[c.organizationId] = (map[c.organizationId] ?? 0) + 1;
    }
    return JSON.stringify(map);
}

// ── ContactOpportunityRoles ───────────────────────────────────────────────────

export async function getRolesByOpportunity(oppId) {
    const v = await tx('contactOpportunityRoles', 'readonly', s => s.index('opportunityId').getAll(oppId));
    return JSON.stringify(v ?? []);
}

export async function getRolesByContact(contactId) {
    const v = await tx('contactOpportunityRoles', 'readonly', s => s.index('contactId').getAll(contactId));
    return JSON.stringify(v ?? []);
}

export async function getRole(contactId, opportunityId) {
    const v = await tx('contactOpportunityRoles', 'readonly', s => s.get([contactId, opportunityId]));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveRole(role) {
    return tx('contactOpportunityRoles', 'readwrite', s => s.put(role));
}

export async function deleteRole(contactId, opportunityId) {
    return tx('contactOpportunityRoles', 'readwrite', s => s.delete([contactId, opportunityId]));
}

export async function deleteRolesByContact(contactId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['contactOpportunityRoles'], 'readwrite');
        t.onerror = () => reject(t.error);
        const store = t.objectStore('contactOpportunityRoles');
        const req = store.index('contactId').getAllKeys(contactId);
        req.onsuccess = () => {
            req.result.forEach(k => store.delete(k));
            t.oncomplete = () => resolve();
        };
        req.onerror = () => reject(req.error);
    });
}

export async function deleteRolesByOpportunity(opportunityId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['contactOpportunityRoles'], 'readwrite');
        t.onerror = () => reject(t.error);
        const store = t.objectStore('contactOpportunityRoles');
        const req = store.index('opportunityId').getAllKeys(opportunityId);
        req.onsuccess = () => {
            req.result.forEach(k => store.delete(k));
            t.oncomplete = () => resolve();
        };
        req.onerror = () => reject(req.error);
    });
}

// ── Opportunities ─────────────────────────────────────────────────────────────

export async function getOpportunitiesByOrganization(orgId) {
    const v = await tx('opportunities', 'readonly', s => s.index('organizationId').getAll(orgId));
    return JSON.stringify(v ?? []);
}

export async function getAllOpportunities() {
    const v = await tx('opportunities', 'readonly', s => s.getAll());
    return JSON.stringify(v ?? []);
}

export async function getOpportunity(id) {
    const v = await tx('opportunities', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveOpportunity(opp) {
    return tx('opportunities', 'readwrite', s => s.put(opp));
}

export async function deleteOpportunity(id) {
    return tx('opportunities', 'readwrite', s => s.delete(id));
}

// ── OpportunityFieldHistory ───────────────────────────────────────────────────

export async function getHistoryByOpportunity(oppId) {
    const v = await tx('opportunityFieldHistory', 'readonly', s => s.index('opportunityId').getAll(oppId));
    return JSON.stringify(v ?? []);
}

export async function saveHistoryEntry(entry) {
    return tx('opportunityFieldHistory', 'readwrite', s => s.put(entry));
}

export async function deleteHistoryByOpportunity(oppId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['opportunityFieldHistory'], 'readwrite');
        t.onerror = () => reject(t.error);
        const store = t.objectStore('opportunityFieldHistory');
        const req = store.index('opportunityId').getAllKeys(oppId);
        req.onsuccess = () => {
            req.result.forEach(k => store.delete(k));
            t.oncomplete = () => resolve();
        };
        req.onerror = () => reject(req.error);
    });
}

// ── Correspondence ────────────────────────────────────────────────────────────

export async function getCorrespondenceByOpportunity(oppId) {
    const v = await tx('correspondence', 'readonly', s => s.index('opportunityId').getAll(oppId));
    return JSON.stringify(v ?? []);
}

export async function getCorrespondence(id) {
    const v = await tx('correspondence', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveCorrespondence(corr) {
    return tx('correspondence', 'readwrite', s => s.put(corr));
}

export async function deleteCorrespondence(id) {
    return tx('correspondence', 'readwrite', s => s.delete(id));
}

export async function deleteCorrespondenceByOpportunity(oppId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['correspondence'], 'readwrite');
        t.onerror = () => reject(t.error);
        const store = t.objectStore('correspondence');
        const req = store.index('opportunityId').getAllKeys(oppId);
        req.onsuccess = () => {
            req.result.forEach(k => store.delete(k));
            t.oncomplete = () => resolve();
        };
        req.onerror = () => reject(req.error);
    });
}

// ── CorrespondenceFiles ───────────────────────────────────────────────────────

export async function getFilesByCorrespondence(corrId) {
    const v = await tx('correspondenceFiles', 'readonly', s => s.index('correspondenceId').getAll(corrId));
    return JSON.stringify(v ?? []);
}

export async function getCorrespondenceFile(id) {
    const v = await tx('correspondenceFiles', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveCorrespondenceFile(file) {
    return tx('correspondenceFiles', 'readwrite', s => s.put(file));
}

export async function deleteCorrespondenceFile(id) {
    return tx('correspondenceFiles', 'readwrite', s => s.delete(id));
}

export async function deleteFilesByCorrespondence(corrId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['correspondenceFiles'], 'readwrite');
        t.onerror = () => reject(t.error);
        const store = t.objectStore('correspondenceFiles');
        const req = store.index('correspondenceId').getAllKeys(corrId);
        req.onsuccess = () => {
            req.result.forEach(k => store.delete(k));
            t.oncomplete = () => resolve();
        };
        req.onerror = () => reject(req.error);
    });
}

// Versioned write for a correspondence record + file additions/removals in one
// atomic transaction. Returns 'success' or 'versionMismatch'.
export async function saveCorrespondenceWithFiles(corr, filesToAdd, fileIdsToDelete) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['correspondence', 'correspondenceFiles'], 'readwrite');
        t.onerror = () => reject(t.error);
        t.oncomplete = () => resolve('success');

        const corrStore = t.objectStore('correspondence');
        const fileStore = t.objectStore('correspondenceFiles');

        const heldVersion = corr.version;
        const getReq = corrStore.get(corr.id);
        getReq.onsuccess = () => {
            const current = getReq.result;
            if (current && current.version !== heldVersion) {
                resolve('versionMismatch');
                return;
            }
            corrStore.put({ ...corr, version: current ? heldVersion + 1 : 1 });
            for (const f of filesToAdd)       fileStore.put(f);
            for (const id of fileIdsToDelete) fileStore.delete(id);
        };
        getReq.onerror = () => reject(getReq.error);
    });
}

export async function getCorrespondenceFileCount(corrId) {
    return tx('correspondenceFiles', 'readonly', s => s.index('correspondenceId').count(corrId));
}

// ── BaseResumes ───────────────────────────────────────────────────────────────

export async function getAllBaseResumes() {
    const v = await tx('baseResumes', 'readonly', s => s.getAll());
    return JSON.stringify(v ?? []);
}

export async function getBaseResume(id) {
    const v = await tx('baseResumes', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveBaseResume(resume) {
    return tx('baseResumes', 'readwrite', s => s.put(resume));
}

export async function deleteBaseResume(id) {
    return tx('baseResumes', 'readwrite', s => s.delete(id));
}

// ── BaseResumeVersions ────────────────────────────────────────────────────────

export async function getVersionsByResume(resumeId) {
    const v = await tx('baseResumeVersions', 'readonly', s => s.index('resumeId').getAll(resumeId));
    return JSON.stringify(v ?? []);
}

export async function getBaseResumeVersion(id) {
    const v = await tx('baseResumeVersions', 'readonly', s => s.get(id));
    return (v === undefined || v === null) ? null : JSON.stringify(v);
}

export async function saveBaseResumeVersion(ver) {
    return tx('baseResumeVersions', 'readwrite', s => s.put(ver));
}

export async function deleteVersionsByResume(resumeId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['baseResumeVersions'], 'readwrite');
        t.onerror = () => reject(t.error);
        const store = t.objectStore('baseResumeVersions');
        const req = store.index('resumeId').getAllKeys(resumeId);
        req.onsuccess = () => {
            req.result.forEach(k => store.delete(k));
            t.oncomplete = () => resolve();
        };
        req.onerror = () => reject(req.error);
    });
}

// ── Lookup tables ─────────────────────────────────────────────────────────────

export async function getLookupValues(tableName) {
    const v = await tx(tableName, 'readonly', s => s.getAll());
    return JSON.stringify(v ?? []);
}

export async function addLookupValue(tableName, record) {
    return tx(tableName, 'readwrite', s => s.put(record));
}

export async function addLookupValueLocked(lockName, tableName, record) {
    return navigator.locks.request(lockName, () => tx(tableName, 'readwrite', s => s.put(record)));
}

// ── Versioned write ───────────────────────────────────────────────────────────

// Atomic read-check-write: reads the current record, verifies record.version
// matches the stored version, then writes with version+1. Returns 'success',
// 'versionMismatch', or 'notFound'. Web Locks wrapper is added in M1-8.
export async function versionedWrite(storeName, record) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction([storeName], 'readwrite');
        t.onerror = () => reject(t.error);
        const store = t.objectStore(storeName);
        const heldVersion = record.version;
        const keyPath = store.keyPath;
        const key = Array.isArray(keyPath)
            ? keyPath.map(k => record[k])
            : record[keyPath];
        const getReq = store.get(key);
        getReq.onsuccess = () => {
            const current = getReq.result;
            if (!current) { resolve('notFound'); return; }
            if (current.version !== heldVersion) { resolve('versionMismatch'); return; }
            const toWrite = { ...record, version: heldVersion + 1 };
            const putReq = store.put(toWrite);
            putReq.onsuccess = () => resolve('success');
            putReq.onerror = () => resolve('error');
        };
        getReq.onerror = () => reject(getReq.error);
    });
}

// ── Cascade deletes ───────────────────────────────────────────────────────────

export async function deleteOrganizationCascade(orgId) {
    const db = await openDb();
    const stores = ['organizations', 'contacts', 'contactOpportunityRoles',
                    'opportunities', 'opportunityFieldHistory', 'correspondence',
                    'correspondenceFiles', 'sessions', 'files'];
    return new Promise((resolve, reject) => {
        const t = db.transaction(stores, 'readwrite');
        t.onerror = () => reject(t.error);
        t.oncomplete = () => resolve();

        const contactStore  = t.objectStore('contacts');
        const roleStore     = t.objectStore('contactOpportunityRoles');
        const oppStore      = t.objectStore('opportunities');
        const histStore     = t.objectStore('opportunityFieldHistory');
        const corrStore     = t.objectStore('correspondence');
        const corrFileStore = t.objectStore('correspondenceFiles');
        const sessionStore  = t.objectStore('sessions');
        const fileStore     = t.objectStore('files');
        const orgStore      = t.objectStore('organizations');

        // 1. Delete all contacts for this org
        contactStore.index('organizationId').getAllKeys(orgId).onsuccess = e => {
            e.target.result.forEach(k => contactStore.delete(k));
        };

        // 2. Delete opportunities → cascade their children
        oppStore.index('organizationId').getAll(orgId).onsuccess = e => {
            e.target.result.forEach(opp => {
                const oid = opp.id;

                // ContactOpportunityRoles
                roleStore.index('opportunityId').getAllKeys(oid).onsuccess = er => {
                    er.target.result.forEach(k => roleStore.delete(k));
                };

                // OpportunityFieldHistory
                histStore.index('opportunityId').getAllKeys(oid).onsuccess = eh => {
                    eh.target.result.forEach(k => histStore.delete(k));
                };

                // Correspondence → files
                corrStore.index('opportunityId').getAll(oid).onsuccess = ec => {
                    ec.target.result.forEach(corr => {
                        corrFileStore.index('correspondenceId').getAllKeys(corr.id).onsuccess = ef => {
                            ef.target.result.forEach(k => corrFileStore.delete(k));
                        };
                        corrStore.delete(corr.id);
                    });
                };

                // Opportunity-linked sessions → generated files
                sessionStore.index('createdAt').getAll().onsuccess = es => {
                    es.target.result
                        .filter(s => s.opportunityId === oid)
                        .forEach(s => {
                            if (s.tailoredResumeFileId) fileStore.delete(s.tailoredResumeFileId);
                            if (s.coverLetterFileId) fileStore.delete(s.coverLetterFileId);
                            sessionStore.delete(s.id);
                        });
                };

                oppStore.delete(oid);
            });
        };

        // 3. Delete org-linked sessions (opportunityId == null) → generated files
        sessionStore.index('createdAt').getAll().onsuccess = es => {
            es.target.result
                .filter(s => s.organizationId === orgId && !s.opportunityId)
                .forEach(s => {
                    if (s.tailoredResumeFileId) fileStore.delete(s.tailoredResumeFileId);
                    if (s.coverLetterFileId) fileStore.delete(s.coverLetterFileId);
                    sessionStore.delete(s.id);
                });
        };

        // 4. Delete the org record itself
        orgStore.delete(orgId);
    });
}

export async function deleteOpportunityCascade(oppId) {
    const db = await openDb();
    const stores = ['opportunities', 'contactOpportunityRoles', 'opportunityFieldHistory',
                    'correspondence', 'correspondenceFiles', 'sessions', 'files'];
    return new Promise((resolve, reject) => {
        const t = db.transaction(stores, 'readwrite');
        t.onerror = () => reject(t.error);
        t.oncomplete = () => resolve();

        const roleStore     = t.objectStore('contactOpportunityRoles');
        const histStore     = t.objectStore('opportunityFieldHistory');
        const corrStore     = t.objectStore('correspondence');
        const corrFileStore = t.objectStore('correspondenceFiles');
        const sessionStore  = t.objectStore('sessions');
        const fileStore     = t.objectStore('files');
        const oppStore      = t.objectStore('opportunities');

        roleStore.index('opportunityId').getAllKeys(oppId).onsuccess = e => {
            e.target.result.forEach(k => roleStore.delete(k));
        };

        histStore.index('opportunityId').getAllKeys(oppId).onsuccess = e => {
            e.target.result.forEach(k => histStore.delete(k));
        };

        corrStore.index('opportunityId').getAll(oppId).onsuccess = e => {
            e.target.result.forEach(corr => {
                corrFileStore.index('correspondenceId').getAllKeys(corr.id).onsuccess = ef => {
                    ef.target.result.forEach(k => corrFileStore.delete(k));
                };
                corrStore.delete(corr.id);
            });
        };

        sessionStore.index('createdAt').getAll().onsuccess = es => {
            es.target.result
                .filter(s => s.opportunityId === oppId)
                .forEach(s => {
                    if (s.tailoredResumeFileId) fileStore.delete(s.tailoredResumeFileId);
                    if (s.coverLetterFileId) fileStore.delete(s.coverLetterFileId);
                    sessionStore.delete(s.id);
                });
        };

        oppStore.delete(oppId);
    });
}

export async function deleteContactCascade(contactId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['contacts', 'contactOpportunityRoles'], 'readwrite');
        t.onerror = () => reject(t.error);
        t.oncomplete = () => resolve();

        const roleStore    = t.objectStore('contactOpportunityRoles');
        const contactStore = t.objectStore('contacts');

        roleStore.index('contactId').getAllKeys(contactId).onsuccess = e => {
            e.target.result.forEach(k => roleStore.delete(k));
        };

        contactStore.delete(contactId);
    });
}

export async function deleteCorrespondenceCascade(corrId) {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['correspondence', 'correspondenceFiles'], 'readwrite');
        t.onerror = () => reject(t.error);
        t.oncomplete = () => resolve();

        const corrFileStore = t.objectStore('correspondenceFiles');
        const corrStore     = t.objectStore('correspondence');

        corrFileStore.index('correspondenceId').getAllKeys(corrId).onsuccess = e => {
            e.target.result.forEach(k => corrFileStore.delete(k));
        };

        corrStore.delete(corrId);
    });
}

// ── Web Locks ─────────────────────────────────────────────────────────────────

// Acquires a chain of locks in order (outer→inner), then runs the version-check
// write inside the innermost lock. Returns 'success', 'versionMismatch', or 'notFound'.
export async function lockedVersionedWrite(lockNames, storeName, record) {
    async function withLocks(names, fn) {
        if (names.length === 0) return fn();
        return navigator.locks.request(names[0], () => withLocks(names.slice(1), fn));
    }
    return withLocks(lockNames, async () => {
        const db = await openDb();
        return new Promise((resolve, reject) => {
            const t = db.transaction([storeName], 'readwrite');
            t.onerror = () => reject(t.error);
            const store = t.objectStore(storeName);
            const heldVersion = record.version;
            const keyPath = store.keyPath;
            const key = Array.isArray(keyPath)
                ? keyPath.map(k => record[k])
                : record[keyPath];
            const getReq = store.get(key);
            getReq.onsuccess = () => {
                const current = getReq.result;
                if (!current) { resolve('notFound'); return; }
                if (current.version !== heldVersion) { resolve('versionMismatch'); return; }
                const toWrite = { ...record, version: heldVersion + 1 };
                const putReq = store.put(toWrite);
                putReq.onsuccess = () => resolve('success');
                putReq.onerror = () => resolve('error');
            };
            getReq.onerror = () => reject(getReq.error);
        });
    });
}

// ── Session / History helpers ─────────────────────────────────────────────────

export async function getOrganizationProjections() {
    const all = await tx('organizations', 'readonly', s => s.getAll());
    return JSON.stringify((all ?? []).map(o => ({ id: o.id, name: o.name })));
}

export async function getOpportunityProjections() {
    const all = await tx('opportunities', 'readonly', s => s.getAll());
    return JSON.stringify((all ?? []).map(o => ({ id: o.id, organizationId: o.organizationId, role: o.role })));
}

export async function deleteAdHocSessionsWithFiles() {
    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(['sessions', 'files'], 'readwrite');
        t.onerror = () => reject(t.error);
        t.oncomplete = () => resolve();

        const sessionStore = t.objectStore('sessions');
        const fileStore    = t.objectStore('files');

        sessionStore.getAll().onsuccess = e => {
            const adHoc = (e.target.result ?? []).filter(s => !s.organizationId);
            for (const s of adHoc) {
                if (s.tailoredResumeFileId) fileStore.delete(s.tailoredResumeFileId);
                if (s.coverLetterFileId)    fileStore.delete(s.coverLetterFileId);
                sessionStore.delete(s.id);
            }
        };
    });
}

// ── Storage stats ─────────────────────────────────────────────────────────────

export async function getStoreBytesAsync(storeNames) {
    const db = await openDb();
    const result = {};
    for (const name of storeNames) {
        if (!db.objectStoreNames.contains(name)) { result[name] = 0; continue; }
        result[name] = await new Promise((resolve, reject) => {
            const t = db.transaction(name, 'readonly');
            const req = t.objectStore(name).openCursor();
            let bytes = 0;
            req.onsuccess = e => {
                const cursor = e.target.result;
                if (cursor) { bytes += JSON.stringify(cursor.value).length; cursor.continue(); }
                else resolve(bytes);
            };
            req.onerror = () => reject(req.error);
        });
    }
    return JSON.stringify(result);
}

// ── Misc ──────────────────────────────────────────────────────────────────────

function _isStandalone() {
    return window.matchMedia('(display-mode: standalone)').matches || !!window.navigator.standalone;
}

function _showDownloadToast(fileName) {
    const el = document.createElement('div');
    el.style.cssText = [
        'position:fixed', 'top:70px', 'left:50%', 'transform:translateX(-50%)',
        'background:#0a1628', 'color:#fff', 'padding:12px 20px', 'border-radius:8px',
        'z-index:9999', 'font-size:14px', 'box-shadow:0 4px 16px rgba(0,0,0,0.5)',
        'max-width:380px', 'width:calc(100% - 32px)', 'text-align:center',
        'transition:opacity 0.4s ease'
    ].join(';');
    el.textContent = `"${fileName}" is downloading — check your Downloads folder.`;
    document.body.appendChild(el);
    setTimeout(() => { el.style.opacity = '0'; setTimeout(() => el.remove(), 400); }, 4000);
}

async function _saveWithPicker(bytes, fileName, mime, extensions) {
    const handle = await window.showSaveFilePicker({
        suggestedName: fileName,
        types: [{ description: 'File', accept: { [mime]: extensions } }]
    });
    const writable = await handle.createWritable();
    await writable.write(bytes);
    await writable.close();
}

export async function downloadFile(fileName, base64Data) {
    const bytes = Uint8Array.from(atob(base64Data), c => c.charCodeAt(0));
    const standalone = _isStandalone();

    if (standalone && 'showSaveFilePicker' in window) {
        const ext = '.' + fileName.split('.').pop().toLowerCase();
        const mime = ext === '.gz' ? 'application/gzip' : 'application/octet-stream';
        try { await _saveWithPicker(bytes, fileName, mime, [ext]); return; }
        catch (e) { if (e.name === 'AbortError') return; }
    }

    const blob = new Blob([bytes], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement('a'), { href: url, download: fileName });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    if (standalone) _showDownloadToast(fileName);
}

// ── Export / Import ───────────────────────────────────────────────────────────

const _backupStores = [
    'sessions', 'files',
    'baseResumes', 'baseResumeVersions',
    'organizations', 'contacts', 'contactOpportunityRoles', 'lookupIndustries', 'lookupContactRoles',
    'opportunities', 'opportunityFieldHistory',
    'correspondence', 'correspondenceFiles',
];

export async function exportAllData() {
    const db = await openDb();
    const payload = { schemaVersion: DB_VERSION, exportedAt: new Date().toISOString(), stores: {} };

    await new Promise((resolve, reject) => {
        const t = db.transaction(_backupStores, 'readonly');
        t.onerror = () => reject(t.error);
        let pending = _backupStores.length;
        for (const name of _backupStores) {
            const req = t.objectStore(name).getAll();
            req.onsuccess = () => {
                payload.stores[name] = req.result ?? [];
                if (--pending === 0) resolve();
            };
            req.onerror = () => reject(req.error);
        }
    });

    const encoded = new TextEncoder().encode(JSON.stringify(payload));
    const cs = new CompressionStream('gzip');
    const writer = cs.writable.getWriter();
    writer.write(encoded);
    writer.close();

    const chunks = [];
    const reader = cs.readable.getReader();
    for (;;) {
        const { done, value } = await reader.read();
        if (done) break;
        chunks.push(value);
    }
    const total = chunks.reduce((s, c) => s + c.length, 0);
    const compressed = new Uint8Array(total);
    let pos = 0;
    for (const c of chunks) { compressed.set(c, pos); pos += c.length; }

    let b64 = '';
    const CHUNK = 8192;
    for (let i = 0; i < compressed.length; i += CHUNK)
        b64 += String.fromCharCode(...compressed.subarray(i, i + CHUNK));
    return btoa(b64);
}

export async function importAllData(base64Data, isGzip) {
    const binaryStr = atob(base64Data);
    let bytes = new Uint8Array(binaryStr.length);
    for (let i = 0; i < binaryStr.length; i++) bytes[i] = binaryStr.charCodeAt(i);

    if (isGzip) {
        const ds = new DecompressionStream('gzip');
        const writer = ds.writable.getWriter();
        writer.write(bytes);
        writer.close();

        const chunks = [];
        const reader = ds.readable.getReader();
        for (;;) {
            const { done, value } = await reader.read();
            if (done) break;
            chunks.push(value);
        }
        const total = chunks.reduce((s, c) => s + c.length, 0);
        const out = new Uint8Array(total);
        let p = 0;
        for (const c of chunks) { out.set(c, p); p += c.length; }
        bytes = out;
    }

    const payload = JSON.parse(new TextDecoder().decode(bytes));
    const storeNames = Object.keys(payload.stores).filter(n => _backupStores.includes(n));

    const db = await openDb();
    return new Promise((resolve, reject) => {
        const t = db.transaction(storeNames, 'readwrite');
        t.onerror = () => reject(t.error);
        t.oncomplete = () => resolve();
        for (const name of storeNames) {
            const store = t.objectStore(name);
            store.clear();
            for (const record of payload.stores[name]) store.put(record);
        }
    });
}

export async function downloadBlob(fileName, base64Data) {
    const bytes = Uint8Array.from(atob(base64Data), c => c.charCodeAt(0));
    const docxMime = 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
    const standalone = _isStandalone();

    if (standalone && 'showSaveFilePicker' in window) {
        try { await _saveWithPicker(bytes, fileName, docxMime, ['.docx']); return; }
        catch (e) { if (e.name === 'AbortError') return; }
    }

    const blob = new Blob([bytes], { type: docxMime });
    const url = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement('a'), { href: url, download: fileName });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
    if (standalone) _showDownloadToast(fileName);
}
