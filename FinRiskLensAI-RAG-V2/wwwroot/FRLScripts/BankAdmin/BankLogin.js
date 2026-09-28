// Bank portal login — client-side validation + AJAX post to /Auth/BankLogin.
$(function () {

    var $form = $("#bankLoginForm");
    var $btn = $("#loginBtn");
    var $alert = $("#loginAlert");
    var $alertText = $("#loginAlertText");

    // ── Show / hide password
    $("#togglePassword").on("click", function () {
        var $pwd = $("#password");
        var isText = $pwd.attr("type") === "text";
        $pwd.attr("type", isText ? "password" : "text");
        $(this).find("i").attr("class", isText ? "bi bi-eye" : "bi bi-eye-slash");
        $(this).attr("aria-label", isText ? "Show password" : "Hide password");
    });

    function setError($input, msgId, message) {
        $input.closest(".blf-input").addClass("is-invalid");
        $("#" + msgId).text(message).addClass("show");
    }

    function clearError($input, msgId) {
        $input.closest(".blf-input").removeClass("is-invalid");
        $("#" + msgId).text("").removeClass("show");
    }

    function hideAlert() {
        $alert.removeClass("show");
        $alertText.text("");
    }

    function showAlert(message) {
        $alertText.text(message || "Unable to sign in. Please try again.");
        $alert.addClass("show");
    }

    // Clear the field error as soon as the user starts correcting it
    $("#userId").on("input", function () { clearError($(this), "userIdError"); hideAlert(); });
    $("#password").on("input", function () { clearError($(this), "passwordError"); hideAlert(); });

    $form.on("submit", function (e) {
        e.preventDefault();
        hideAlert();

        var $userId = $("#userId");
        var $password = $("#password");
        var userId = ($userId.val() || "").trim();
        var password = $password.val() || "";
        var ok = true;

        if (userId === "") {
            setError($userId, "userIdError", "User ID is required.");
            ok = false;
        }
        if (password === "") {
            setError($password, "passwordError", "Password is required.");
            ok = false;
        }
        if (!ok) return;

        $btn.prop("disabled", true).addClass("loading");

        $.ajax({
            url: "/Auth/BankLogin",
            type: "POST",
            data: {
                userId: userId,
                password: password,
                __RequestVerificationToken: $form.find("input[name='__RequestVerificationToken']").val()
            },
            success: function (res) {
                if (res && res.status) {
                    // Keep the button spinning through the redirect
                    window.location.href = res.redirectUrl || "/BankAdmin/Dashboard";
                    return;
                }
                $btn.prop("disabled", false).removeClass("loading");
                showAlert(res && res.message);
                $password.val("").focus();
            },
            error: function () {
                $btn.prop("disabled", false).removeClass("loading");
                showAlert("Could not reach the server. Please try again.");
            }
        });
    });
});
