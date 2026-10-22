// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

export function open(dialog) {
    if (!dialog.dataset.initialized) {
        dialog.addEventListener('cancel', event => {
            if (dialog.dataset.busy === 'true') event.preventDefault();
        });
        dialog.dataset.initialized = 'true';
    }
    if (!dialog.open) dialog.showModal();
    dialog.querySelector('#website-name')?.focus();
}

export function close(dialog) {
    if (dialog.open) dialog.close();
}
