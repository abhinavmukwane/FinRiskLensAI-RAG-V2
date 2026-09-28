
$(document).ready(function () {

    var $otpInputs = $(".otp-input");
    var $otpProgress = $("#otpProgress");
    var $submitBtn = $("#submitBtn");
    var $resendBtn = $("#resendBtn");
    var $resendTimer = $("#resendTimer");
    var resendInterval = null;

    function getOtpValue() {
        var val = "";
        $otpInputs.each(function () {
            val += $(this).val();
        });
        return val;
    }

    function showToast(msg) {
        $('#toastMsg').text(msg);
        new bootstrap.Toast(document.getElementById('liveToast'), { delay: 2600 }).show();
    }

    function updateOtpProgress() {
        var filled = 0;
        $otpInputs.each(function () {
            if ($(this).val().length === 1) filled++;
        });
        $otpProgress.css("width", (filled / $otpInputs.length * 100) + "%");
        $submitBtn.prop("disabled", filled !== $otpInputs.length);
    }

    function clearOtpBoxes() {
        $otpInputs.val("").removeClass("filled error");
        updateOtpProgress();
    }

    function startResendTimer() {
        var t = 30;
        $resendTimer.text(t);
        $resendBtn.prop("disabled", true).html('Resend in <span id="resendTimer">' + t + '</span>s');
        $resendBtn.html('Resend in <span id="resendTimer">' + t + '</span>s');
        clearInterval(resendInterval);
        resendInterval = setInterval(function () {
            t -= 1;
            $("#resendTimer").text(t);
            if (t <= 0) {
                clearInterval(resendInterval);
                $resendBtn.prop("disabled", false).text("Resend OTP");
            }
        }, 1000);
    }

    // ---------- OTP box interactions ----------
    $otpInputs.on("input", function () {

        var val = $(this).val().replace(/[^0-9]/g, "").slice(0, 1);
        $(this).val(val);
        $(this).toggleClass("filled", val.length === 1);
        $(this).removeClass("error");

        if (val) {
            var next = $(this).closest(".otp-row").find(".otp-input").eq($otpInputs.index(this) + 1);
            if (next.length) next.focus();
        }

        updateOtpProgress();
    });

    $otpInputs.on("keydown", function (e) {
        if (e.key === "Backspace" && $(this).val() === "") {
            var idx = $otpInputs.index(this);
            if (idx > 0) $otpInputs.eq(idx - 1).focus();
        }
    });

    $otpInputs.on("paste", function (e) {
        e.preventDefault();
        var text = (e.originalEvent.clipboardData || window.clipboardData).getData("text");
        var digits = (text.match(/\d/g) || []).slice(0, $otpInputs.length);

        digits.forEach(function (d, i) {
            $otpInputs.eq(i).val(d).addClass("filled");
        });

        var nextIndex = Math.min(digits.length, $otpInputs.length - 1);
        $otpInputs.eq(nextIndex).focus();
        updateOtpProgress();
    });

    $resendBtn.on("click", function () {
        if ($(this).prop("disabled")) return;
        $("#generateOtpBtn").trigger("click", [true]);
    });


    // ---------- Copy OTP (dev modal) ----------
    $("#copyOtpBtn").on("click", function () {
        var $btn = $(this);
        var otp = $("#otpDisplayValue").text().trim();

        if (!otp) return;

        navigator.clipboard.writeText(otp).then(function () {
            var original = $btn.html();
            $btn.html('<i class="bi bi-check2"></i>');
            setTimeout(function () {
                $btn.html(original);
            }, 1200);
        });
    });

    // ---------- Generate OTP ----------
    $("#generateOtpBtn").click(function (e, isResend) {

        var email = $("#email").val().trim();

        if (email == "") {
            $("#email").addClass("is-invalid");
            return;
        }

        $("#email").removeClass("is-invalid");

        var model = {
            Email: email,
            theme: (localStorage.getItem('frl-theme') || 'theme1')
        };

        var $btn = $("#generateOtpBtn");
        var originalHtml = $btn.html();

        $.ajax({

            url: "/Auth/GenCustomerOtp",
            type: "POST",
            data: model,
            beforeSend: function () {

                $btn.prop("disabled", true);
                $btn.html('<i class="bi bi-arrow-repeat"></i> <span>Sending OTP...</span>');
            },
            success: function (res) {

                $btn.prop("disabled", false);
                $btn.html(originalHtml);

                if (res.status) {

                    // Show OTP in modal (Development only)
                    $("#otpDisplayValue").text(res.otp);

                    var otpModal = new bootstrap.Modal(document.getElementById("otpDisplayModal"));
                    otpModal.show();

                    // Hide Generate OTP button
                    $("#generateOtpBtn").addClass("d-none");

                    // Lock Email textbox
                    $("#email").prop("readonly", true);

                    // Reset OTP boxes and show OTP section
                    clearOtpBoxes();
                    $("#box2").removeClass("d-none");

                    //// Smooth scroll to OTP section
                    //$('html, body').animate({
                    //    scrollTop: $("#box2").offset().top - 80
                    //}, 300);

                    // Focus first OTP box
                    $(".otp-input").first().focus();

                    // Start resend countdown
                    startResendTimer();
                }
                else {
                    showToast(res.message);
                    if (res.redirectUrl) {
                        setTimeout(function () { window.location.href = res.redirectUrl; }, 1500);
                    }
                }
            },

            error: function () {

                $btn.prop("disabled", false);
                $btn.html(originalHtml);

                showToast("Something went wrong while sending OTP.");
            }

        });

    });


    $("#submitBtn").click(function () {

        var model = {
            Email: $("#email").val(),
            OTP: getOtpValue()
        };

        $.ajax({

            url: "/Auth/ValidateUserOtp",
            type: "POST",
            data: model,
            beforeSend: function () {

                $("#submitBtn").prop("disabled", true);
                $("#submitLabel").text("Validating...");
            },
            success: function (res) {
                $("#submitLabel").text("Login to Portal");

                if (res.status) {
                    window.location.href = "/Dashboard/CustDashboard";
                }
                else {

                    alert(res.message);

                    $otpInputs.addClass("error");
                    clearOtpBoxes();
                    $otpInputs.first().focus();
                    $("#submitBtn").prop("disabled", true);
                }
            },
            error: function () {

                $("#submitBtn").prop("disabled", false);
                $("#submitLabel").text("Login to Portal");
                alert("Something went wrong.");
            }

        });

    });



    // ---------- Resend OTP Click ----------
    $("#resendBtn").click(function (e) {

        e.preventDefault();

        if ($(this).prop("disabled")) return;

        var email = $("#email").val().trim();

        if (email == "") {
            showToast("Email not found. Please restart the process.");
            return;
        }

        var model = {
            Email: email,
            theme: (localStorage.getItem('frl-theme') || 'theme1')
        };

        var $btn = $(this);
        var originalHtml = $btn.html();

        $.ajax({

            url: "/Auth/GenCustomerOtp",
            type: "POST",
            data: model,

            beforeSend: function () {
                $btn.prop("disabled", true);
                $btn.html('<i class="bi bi-arrow-repeat"></i> Sending...');
            },

            success: function (res) {

                if (res.status) {

                    // Show OTP in modal (Development only)
                    $("#otpDisplayValue").text(res.otp);

                    var otpModal = new bootstrap.Modal(document.getElementById("otpDisplayModal"));
                    otpModal.show();

                    clearOtpBoxes();
                    $(".otp-input").first().focus();

                    showToast("A new OTP has been sent.");

                    startResendTimer();
                }
                else {
                    $btn.prop("disabled", false);
                    $btn.html(originalHtml);
                    showToast(res.message || "Failed to resend OTP.");
                }
            },

            error: function () {
                $btn.prop("disabled", false);
                $btn.html(originalHtml);
                showToast("Something went wrong while resending OTP.");
            }

        });

    });

});

