import assert from "node:assert/strict";
import test from "node:test";

import { drive, driveClick } from "../../../src/DesignSystem.Stories/wwwroot/storyDriver.js";

class TestMutationObserver {
	constructor(callback) {
		this.callback = callback;
	}

	disconnect() {
	}

	observe(root) {
		root.observer = this;
	}

	takeRecords() {
		return [];
	}
}

globalThis.MutationObserver = TestMutationObserver;

function formOwnedBy(root) {
	const listeners = [];
	return {
		addEventListener(name, listener) {
			if (name === "submit")
				listeners.push(listener);
		},
		querySelectorAll() {
			return [];
		},
		requestSubmit() {
			for (const listener of listeners)
				listener(new Event("submit", { cancelable: true }));
			setTimeout(() => root.observer.callback(), 0);
		}
	};
}

test("drive submits only the form beneath its supplied story root", async () => {
	const staleRoot = { observer: undefined };
	const currentRoot = {
		observer: undefined,
		querySelector(selector) {
			assert.equal(selector, "form");
			return currentForm;
		},
		querySelectorAll() {
			return [];
		}
	};
	const staleForm = formOwnedBy(staleRoot);
	const currentForm = formOwnedBy(currentRoot);
	let staleSubmissions = 0;
	let currentSubmissions = 0;
	const submitStale = staleForm.requestSubmit;
	staleForm.requestSubmit = () => {
		staleSubmissions++;
		submitStale();
	};
	const submitCurrent = currentForm.requestSubmit;
	currentForm.requestSubmit = () => {
		currentSubmissions++;
		submitCurrent();
	};
	globalThis.document = {
		querySelector(selector) {
			assert.equal(selector, "form");
			return staleForm;
		}
	};

	const driven = await drive(currentRoot, false, "designer@example.com", "aaaaaaaa");

	assert.equal(driven, true);
	assert.equal(currentSubmissions, 1);
	assert.equal(staleSubmissions, 0);
});

test("driveClick clicks the story's button and resolves only after the observer settles", async () => {
	let clicks = 0;
	const root = {
		observer: undefined,
		querySelector(selector) {
			assert.equal(selector, "button");
			return button;
		}
	};
	const button = {
		click() {
			clicks++;
			// The DOM change the click causes lands on the observer AFTER the click — the settle
			// promise must not resolve before this callback runs.
			setTimeout(() => root.observer.callback(), 0);
		}
	};

	const driven = await driveClick(root);

	assert.equal(driven, true);
	assert.equal(clicks, 1);
});

test("driveClick resolves false when no button ever appears", async () => {
	const root = {
		observer: undefined,
		querySelector() {
			return null;
		}
	};

	const driven = await driveClick(root);

	assert.equal(driven, false);
});
