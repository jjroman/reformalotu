// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    if (document.body.classList.contains("identity-login-page")) {
        const loginForm = document.getElementById("account");
        const rememberRow = loginForm?.querySelector(".checkbox");
        const submitButton = loginForm?.querySelector("#login-submit");
        const forgotParagraph = loginForm?.querySelector("#forgot-password")?.closest("p");
        const registerParagraph = loginForm?.querySelector('a[href*="/Account/Register"]')?.closest("p");
        const resendParagraph = loginForm?.querySelector("#resend-confirmation")?.closest("p");

        if (
            !loginForm ||
            !rememberRow ||
            !submitButton ||
            !forgotParagraph ||
            !registerParagraph ||
            !resendParagraph
        ) {
            console.error("The login links could not be positioned because expected Identity elements are missing.");
        } else {
            const originalLinksContainer = forgotParagraph.parentElement;
            const optionsRow = document.createElement("div");
            optionsRow.className = "identity-login-options";
            optionsRow.append(rememberRow, forgotParagraph);
            loginForm.insertBefore(optionsRow, submitButton.parentElement);

            const footerLinks = document.createElement("div");
            footerLinks.className = "identity-login-footer-links";
            footerLinks.append(registerParagraph, resendParagraph);

            const linkDestination = document.getElementById("external-account") ?? loginForm;
            linkDestination.insertAdjacentElement("afterend", footerLinks);

            if (originalLinksContainer && originalLinksContainer.childElementCount === 0) {
                originalLinksContainer.remove();
            }
        }
    }

    document.querySelectorAll("textarea[data-markdown-editor]").forEach((textarea) => {
        if (!(textarea instanceof HTMLTextAreaElement)) {
            return;
        }

        const editor = document.createElement("div");
        editor.className = "project-markdown-editor";
        textarea.parentNode?.insertBefore(editor, textarea);
        editor.append(textarea);

        const toolbar = document.createElement("div");
        toolbar.className = "project-markdown-toolbar";
        toolbar.setAttribute("role", "toolbar");
        toolbar.setAttribute("aria-label", "Herramientas de formato Markdown");
        editor.insertBefore(toolbar, textarea);

        const tools = [
            { label: "Negrita", prefix: "**", suffix: "**", placeholder: "texto en negrita" },
            { label: "Cursiva", prefix: "*", suffix: "*", placeholder: "texto en cursiva" },
            { label: "Título", prefix: "## ", line: true, placeholder: "Título" },
            { label: "Lista", prefix: "- ", line: true, placeholder: "Elemento de lista" },
            { label: "Enlace", prefix: "[", suffix: "](https://)", placeholder: "texto del enlace" }
        ];

        tools.forEach((tool) => {
            const button = document.createElement("button");
            button.type = "button";
            button.textContent = tool.label;
            button.addEventListener("click", () => {
                const start = textarea.selectionStart;
                const end = textarea.selectionEnd;
                const selectedText = textarea.value.slice(start, end);

                if (tool.line) {
                    const lineStart = textarea.value.lastIndexOf("\n", start - 1) + 1;
                    const lineEndIndex = textarea.value.indexOf("\n", end);
                    const lineEnd = lineEndIndex === -1 ? textarea.value.length : lineEndIndex;
                    const selectedLines = textarea.value.slice(lineStart, lineEnd) || tool.placeholder;
                    const formattedLines = selectedLines
                        .split("\n")
                        .map((line) => `${tool.prefix}${line}`)
                        .join("\n");
                    textarea.setRangeText(formattedLines, lineStart, lineEnd, "select");
                } else {
                    const content = selectedText || tool.placeholder;
                    const formattedText = `${tool.prefix}${content}${tool.suffix}`;
                    textarea.setRangeText(formattedText, start, end, "select");
                    textarea.setSelectionRange(
                        start + tool.prefix.length,
                        start + tool.prefix.length + content.length
                    );
                }

                textarea.focus();
                textarea.dispatchEvent(new Event("input", { bubbles: true }));
            });
            toolbar.append(button);
        });
    });
})();
