// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

(() => {
    if (!document.body.classList.contains("identity-login-page")) {
        return;
    }

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
        return;
    }

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
})();
