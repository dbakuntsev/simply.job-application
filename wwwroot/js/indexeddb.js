const DB_NAME = 'SimplyJobApplication';
const DB_VERSION = 2;

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

export function downloadFile(fileName, base64Data) {
    const bytes = Uint8Array.from(atob(base64Data), c => c.charCodeAt(0));
    const blob = new Blob([bytes], { type: 'application/octet-stream' });
    const url = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement('a'), { href: url, download: fileName });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}

export function downloadBlob(fileName, base64Data) {
    const bytes = Uint8Array.from(atob(base64Data), c => c.charCodeAt(0));
    const blob = new Blob([bytes], {
        type: 'application/vnd.openxmlformats-officedocument.wordprocessingml.document'
    });
    const url = URL.createObjectURL(blob);
    const a = Object.assign(document.createElement('a'), { href: url, download: fileName });
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
}
