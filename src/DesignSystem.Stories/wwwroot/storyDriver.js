// Story-side play function: find the story's form, optionally fill its text/password inputs, submit.
// FluentUI Blazor components may project their native <input> into a shadow root, so the search
// descends one shadow level; the retry loop absorbs WASM canvas settling and web-component upgrade.
const maxTries = 40;
const delayMs = 50;
const minimumSettleMs = 500;
const quietMs = 250;
const settleTimeoutMs = 5000;

function waitForPostSubmitSettle(root, requirePostSubmitTurn) {
	let barrierPassed = !requirePostSubmitTurn;
	let observed = false;
	let quietTimer;
	let submittedAt = performance.now();
	let lastMutationAt = submittedAt;
	let resolveCompletion;
	let rejectCompletion;

	const completion = new Promise((resolve, reject) => {
		resolveCompletion = resolve;
		rejectCompletion = reject;
	});

	const finish = () => {
		const now = performance.now();
		const minimumRemaining = minimumSettleMs - (now - submittedAt);
		const quietRemaining = quietMs - (now - lastMutationAt);
		if (!observed || minimumRemaining > 0 || quietRemaining > 0) {
			quietTimer = setTimeout(finish, Math.max(minimumRemaining, quietRemaining, quietMs));
			return;
		}

		clearTimeout(timeout);
		observer.disconnect();
		resolveCompletion();
	};

	const observer = new MutationObserver(() => {
		if (!barrierPassed)
			return;

		observed = true;
		lastMutationAt = performance.now();
		clearTimeout(quietTimer);
		quietTimer = setTimeout(finish, quietMs);
	});

	const timeout = setTimeout(() => {
		clearTimeout(quietTimer);
		observer.disconnect();
		rejectCompletion(new Error('StoryDriver observed no settled post-submit DOM activity.'));
	}, settleTimeoutMs);

	observer.observe(root, { attributes: true, characterData: true, childList: true, subtree: true });

	return {
		completion,
		markSubmitted() {
			submittedAt = performance.now();
			lastMutationAt = submittedAt;
			if (requirePostSubmitTurn)
				queueMicrotask(() => {
					observer.takeRecords();
					barrierPassed = true;
				});
		}
	};
}

function inputsOf(root) {
	const inputs = [...root.querySelectorAll("input")];
	for (const el of root.querySelectorAll("*"))
		if (el.shadowRoot)
			inputs.push(...el.shadowRoot.querySelectorAll("input"));
	return inputs;
}

export async function drive(fill, email, password) {
	for (let attempt = 0; attempt < maxTries; attempt++) {
		const form = document.querySelector("form");
		if (form) {
			if (fill)
				for (const input of inputsOf(form)) {
					const type = (input.getAttribute("type") ?? "text").toLowerCase();
					if (type !== "text" && type !== "email" && type !== "password")
						continue;
					input.value = type === "password" ? password : email;
					input.dispatchEvent(new Event("input", { bubbles: true, composed: true }));
					input.dispatchEvent(new Event("change", { bubbles: true, composed: true }));
				}
			// Native constraint validation would block an empty Required form before Blazor ever saw
			// the submit event (shadow-rooted invalid controls aren't focusable) — Blazilla owns
			// validation in this catalog, the browser doesn't.
			form.noValidate = true;
			// requestSubmit() races Blazor's own submit interception (the WASM interactive delegator
			// and/or enhanced-navigation's own listener) -- whichever attaches to the form first wins.
			// If neither has attached yet, the browser's native default action fires: a real navigation
			// that escapes the story's iframe and boots the whole catalog inside it. This capture-phase
			// listener is registered synchronously, before dispatch, so it is never subject to that race;
			// it blocks the browser's default action unconditionally without stopping propagation, so
			// Blazor's own listener(s) still see the event and process the submit normally once attached.
			const settled = waitForPostSubmitSettle(document.body, fill);
			form.addEventListener("submit", event => event.preventDefault(), { capture: true, once: true });
			form.requestSubmit();
			settled.markSubmitted();
			await settled.completion;
			return true;
		}
		await new Promise(resolve => setTimeout(resolve, delayMs));
	}
	return false;
}
