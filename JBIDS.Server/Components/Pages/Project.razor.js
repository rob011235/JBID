// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

export function open(dialog) {
    if (!dialog.open) dialog.showModal();
    dialog.scrollTop = 0;
    dialog.querySelector('.btn-close')?.focus();
}

export function close(dialog) {
    if (dialog.open) dialog.close();
}
