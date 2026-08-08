// Story-side play function: find the story's form, optionally fill its text/password inputs, submit.
// FluentUI Blazor components may project their native <input> into a shadow root, so the search
// descends one shadow level; the retry loop absorbs WASM canvas settling and web-component upgrade.
const maxTries = 40, delayMs = 50;

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
			form.requestSubmit();
			return true;
		}
		await new Promise(resolve => setTimeout(resolve, delayMs));
	}
	return false;
}
