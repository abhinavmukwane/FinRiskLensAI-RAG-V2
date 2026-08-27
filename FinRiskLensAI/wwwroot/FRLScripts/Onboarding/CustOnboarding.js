
$(function () {

    $(".rv-mobile-input").prop("disabled", true);

    // ---------- HELPERS ----------
    function showToast(msg) {
        $('#toastMsg').text(msg);
        new bootstrap.Toast(document.getElementById('liveToast'), { delay: 2600 }).show();
    }

    // ---------- LOGIN MODE DETECTION ----------
    const urlParams = new URLSearchParams(window.location.search);
    const isLoginMode = urlParams.get('mode') === 'login';

    if (isLoginMode) {
        $('body').addClass('login-mode');

        // Adjust Step 1 UI text
        $('#step1 .eyebrow').text('Secure Access');
        $('#step1 .stage-title').text('Login to your MSME Credit Dashboard');
        $('#step1 label[for="udyamInput"]').text('Registered Udyam Number or Mobile');
        $('#udyamInput').attr('placeholder', 'UDYAM-XX-00-0000000 or 10-digit Mobile');
        $('#step1 .help-text').html('<i class="bi bi-info-circle-fill"></i> Enter the Udyam registration ID or mobile number associated with your profile.');
        $('#fetchBtnLabel').html('<i class="bi bi-arrow-right-circle me-1"></i> Send OTP');

        // Adjust Step 3 UI text
        $('#step3 .eyebrow').text('Authentication');
        $('#step3 .stage-title').text('Enter Verification Code');
        $('#step3 .stage-desc').html('Enter the 6-digit OTP sent to your registered contact.');

        // Adjust Step 4 UI text
        $('#step4 .eyebrow').text('Access Granted');
        $('#step4 .stage-title').text('Login Successful 🎉');
        $('#step4 .stage-desc').text('Welcome back! Your identity has been authenticated.');
        $('#step4 .stage-body h3').text('Welcome Back!');
        $('#step4 .stage-body .text-muted').text('Your secure session has been initialized. Click below to load your credit risk intelligence dashboard.');
        $('#btnGoToDashboard').html('<i class="bi bi-speedometer2 me-1"></i> Load Dashboard');
    }

    // 4-step rail: fill percentages at 0%, 33%, 66%, 100%
    var fillMap = { 1: 0, 2: 33, 3: 66, 4: 100 };

    function goToRailStep(n) {
        if (isLoginMode) return; // Rail hidden in login mode
        $('.rail-step').each(function () {
            var s = parseInt($(this).data('step'));
            $(this).removeClass('active done');
            if (s < n) {
                $(this).addClass('done').find('.node').html('<i class="bi bi-check-lg"></i>');
            } else if (s === n) {
                $(this).addClass('active');
            }
        });
        $('#railFill').css('width', fillMap[n] + '%');
    }

    // Store original icons
    $('.rail-step .node').each(function () { $(this).data('orig', $(this).html()); });

    // ---------- CREDENTIAL MASK ----------
    $('#udyamInput').on('input', function () {
        let rawVal = this.value;
        if (isLoginMode && /^[0-9]/.test(rawVal)) {
            // Mobile input mask
            let clean = rawVal.replace(/[^0-9]/g, '').slice(0, 10);
            this.value = clean;
        } else {
            // Udyam input mask
            let v = rawVal.toUpperCase().replace(/[^A-Z0-9]/g, '');
            let out = 'UDYAM';
            if (v.startsWith('UDYAM')) v = v.slice(5);
            if (v.length > 0) out += '-' + v.slice(0, 2);
            if (v.length > 2) out += '-' + v.slice(2, 4);
            if (v.length > 4) out += '-' + v.slice(4, 11);
            this.value = out;
        }
    });

    // ---------- STEP 1 → LOADER → STEP 2 / STEP 3 ----------
    $('#btnFetch').on('click', function () {
        const val = $('#udyamInput').val().trim();

        if (isLoginMode) {
            const isUdyam = /^UDYAM-[A-Z]{2}-\d{2}-\d{7}$/.test(val);
            const isMobile = /^\d{10}$/.test(val);

            if (!isUdyam && !isMobile) {
                $('#udyamError').removeClass('d-none').find('span').text('Please enter a valid Udyam number (UDYAM-XX-00-0000000) or a 10-digit mobile number.');
                return;
            }
            $('#udyamError').addClass('d-none');

            // Hide Step 1, show loader
            $('#step1').addClass('d-none');
            $('#fetchLoader .screen-title').text('Authenticating session credentials…');
            $('#ls1').html('<i class="bi bi-check-circle-fill"></i> Checking credential registry');
            $('#ls2').html('<i class="bi bi-check-circle-fill"></i> Locating active credit files');
            $('#ls3').html('<i class="bi bi-check-circle-fill"></i> Sending security verification code');
            $('#ls1, #ls2, #ls3').removeClass('active done');
            $('#fetchLoader').show();
            $('html,body').animate({ scrollTop: 0 }, 200);

            // Adapt times for simulation
            setTimeout(function () {
                $('#ls1').addClass('active');
            }, 200);
            setTimeout(function () {
                $('#ls1').removeClass('active').addClass('done');
                $('#ls2').addClass('active');
            }, 900);
            setTimeout(function () {
                $('#ls2').removeClass('active').addClass('done');
                $('#ls3').addClass('active');
            }, 1800);
            setTimeout(function () {
                $('#ls3').removeClass('active').addClass('done');
            }, 2700);

            // Skip Step 2, show Step 3 (OTP)
            setTimeout(function () {
                $('#fetchLoader').hide();
                $('#step3').removeClass('d-none');
                if (isMobile) {
                    $('#otpEmailTarget').text('+91 ' + val.slice(0, 2) + '******' + val.slice(8));
                } else {
                    $('#otpEmailTarget').text('registered contact details');
                }

                if (isUdyam) {
                    $('#dash_entUdyam').text(val);
                } else {
                    $('#dash_entUdyam').text('UDYAM-MH-20-0012345');
                }

                startOtpTimer();
                $('.otp-input').first().focus();
                showToast('OTP sent to your registered contact.');
                $('html,body').animate({ scrollTop: 0 }, 300);
            }, 3200);

        } else {

            const pattern = /^UDYAM-[A-Z]{2}-\d{2}-\d{7}$/;

            if (!pattern.test(val)) {
                $('#udyamError')
                    .removeClass('d-none')
                    .find('span')
                    .text('Please enter a valid Udyam number in the format UDYAM-XX-00-0000000.');
                return;
            }

            $('#udyamError').addClass('d-none');

            // Hide Step1 & Show Loader
            $('#step1').addClass('d-none');

            $('#fetchLoader .screen-title').text('Initiating the Udyam check...');

            $('#ls1').html('<i class="bi bi-check-circle-fill"></i> Validating Enterprise Identity');
            $('#ls2').html('<i class="bi bi-check-circle-fill"></i> Retrieving Official Records');
            $('#ls3').html('<i class="bi bi-check-circle-fill"></i> Validating Udyam Details');

            $('#ls1,#ls2,#ls3').removeClass('active done');

            $('#fetchLoader').show();

            $('html,body').animate({ scrollTop: 0 }, 200);

            // Animation
            setTimeout(function () {
                $('#ls1').addClass('active');
            }, 200);

            setTimeout(function () {
                $('#ls1').removeClass('active').addClass('done');
                $('#ls2').addClass('active');
            }, 900);

            setTimeout(function () {
                $('#ls2').removeClass('active').addClass('done');
                $('#ls3').addClass('active');
            }, 1800);

            // Call API
            $.ajax({

                url: "/Onboarding/FetchUdyam",
                type: "POST",
                data: {
                    uan: val
                },

                success: function (res) {
                    if (res.isRegistered) {

                        $('#fetchLoader').hide();
                        $('#step1').removeClass('d-none');

                        showToast(res.message);

                        setTimeout(function () {
                            window.location.href = "/Auth/CustLogin";
                        }, 1500);

                        return;
                    }

                    if (res.status) {

                        $('#ls3').removeClass('active').addClass('done');

                        loadUdyamDetails(res.uan)
                            .done(function (response) {

                                if (!response.status) {
                                    showToast(response.message);
                                    return;
                                }

                                var d = response.data;
                                $("#rv_entName").text(d.enterpriseName);
                                $("#rv_orgType").text(d.organizationType);
                                $("#rv_dob").text(d.dateOfIncorporation);
                                $("#rv_msme").text(d.enterpriseType);
                                $("#rv_msme").text(d.enterpriseType);


                                $("#rv_udyam_number").text(d.udyamNumber);
                                $("#rv_gstnNumber").text(d.gstin);
                                $("#rv_panNo").text(d.pan);
                                $("#hdnMsmeEnquiryID").val(d.msmeEnquiryID);

                                // d.email/d.mobile are already masked server-side (GetUdyamDetails) —
                                // the real values only ever exist here encrypted, in the hidden
                                // fields, and are decrypted server-side at GenerateOtp.
                                $("#rv_email").val(d.email);
                                $("#mobileInput").val(d.mobile);
                                $("#rv_email_enc").val(response.emailEnc);
                                $("#mobileInput_enc").val(response.mobileEnc);
                                $("#rv_address").text(d.address);
                                checkProceedEnabled();

                                // Show Step 2 only after data is loaded
                                $('#fetchLoader').hide();
                                $('#step1').addClass('d-none');
                                $('#step2').removeClass('d-none');

                                goToRailStep(2);

                                showToast('Enterprise details fetched successfully.');

                                $('html,body').animate({
                                    scrollTop: 0
                                }, 300);
                            })
                            .fail(function () {

                                $('#fetchLoader').hide();
                                $('#step1').removeClass('d-none');

                                showToast("Failed to load enterprise details.");
                            });
                    }

                    else {

                        $('#fetchLoader').hide();

                        $('#step1').removeClass('d-none');

                        showToast(res.message || "Unable to fetch Udyam details.");
                    }
                },

                error: function () {

                    $('#fetchLoader').hide();

                    $('#step1').removeClass('d-none');

                    showToast("Something went wrong while fetching Udyam details.");
                }

            });
        }

    });

    function loadUdyamDetails(uan) {

        return $.ajax({
            url: '/Onboarding/GetUdyamDetails',
            type: 'GET',
            data: { uan: uan }
        });
    }

    // ---------- REVIEW STEP: enable Proceed button ----------
    // Mobile is read-only and shows the masked value (98******21), so it can
    // no longer be regex-validated as raw digits — the encrypted hidden field
    // being populated (from a successful Udyam fetch) is the real signal.
    function checkProceedEnabled() {
        const mobileOk = !!$('#mobileInput_enc').val();
        const consentOk = $('#consentCheck').is(':checked');
        $('#btnProceedToOtp').prop('disabled', !(mobileOk && consentOk));
    }
    $('#consentCheck').on('change', checkProceedEnabled);




    $('#btnProceedToOtp').on('click', function () {
        var model = {
            MsmeEnquiryID: $('#hdnMsmeEnquiryID').val(),
            UdyamNumber: $('#rv_udyam_number').text().trim(),
            // Masked display values — the server ignores these in favor of
            // emailEnc/mobileEnc below whenever they're present, decrypting
            // the real values only at the point of sending/saving them.
            MobileNumber: $('#mobileInput').val(),
            Email: $('#rv_email').val(),
            GstinNumber: $('#rv_gstnNumber').text().trim(),
            PanNumber: $('#rv_panNo').text().trim()
        };
        const $btn = $(this);
        $btn.prop('disabled', true)
            .html('<span class="spin-loader"></span> Sending OTP...');
        var nameOfEnterprise = $('#rv_entName').text().trim();
        $.ajax({
            url: '/Onboarding/GenerateOtp',
            type: 'POST',
            data: {
                model, nameOfEnterprise, theme: (localStorage.getItem('frl-theme') || 'theme1'),
                emailEnc: $('#rv_email_enc').val(),
                mobileEnc: $('#mobileInput_enc').val()
            },
            success: function (response) {
                $btn.prop('disabled', false)
                    .html('<i class="bi bi-send me-1"></i> Proceed & Send OTP');
                if (response.status) {
                    $('#step2').addClass('d-none');
                    $('#step3').removeClass('d-none');
                    $('#otpEmailTarget').text(model.Email);
                    goToRailStep(3);
                    startOtpTimer();
                    $('.otp-input').first().focus();
                    showToast(response.message);
                    $('html,body').animate({ scrollTop: 0 }, 300);
                    if (response.otp) {
                        $('#otpDisplayValue').text(response.otp);
                        var otpModal = new bootstrap.Modal(document.getElementById('otpDisplayModal'));
                        otpModal.show();
                    }
                }
                else {
                    $('#reviewError').removeClass('d-none');
                    $('#reviewError span').text(response.message);
                    showToast(response.message);
                    if (response.clearFields) {
                        $('#rv_email').val('');
                        $('#mobileInput').val('');
                        $('#btnProceedToOtp').prop('disabled', true);
                        $('#rv_email').focus();
                    }
                }
            },
            error: function () {
                $btn.prop('disabled', false)
                    .html('<i class="bi bi-send me-1"></i> Proceed & Send OTP');
                $('#reviewError').removeClass('d-none');
                $('#reviewError span').text('Something went wrong.');
            }
        });
    });





    // ---------- STEP 2 → STEP 3 (send OTP) ----------

    //$('#btnProceedToOtp').on('click', function () {
    //    var model = {
    //        MsmeEnquiryID: $('#hdnMsmeEnquiryID').val(),
    //        UdyamNumber: $('#rv_udyam_number').text().trim(),
    //        MobileNumber: $('#mobileInput').val(),
    //        Email: $('#rv_email').val(),
    //        GstinNumber: $('#rv_gstnNumber').text().trim(),
    //        PanNumber: $('#rv_panNo').text().trim()
    //    };
    //    const $btn = $(this);
    //    $btn.prop('disabled', true)
    //        .html('<span class="spin-loader"></span> Registering...');

    //    var nameOfEnterprise = $('#rv_entName').text().trim();

    //    $.ajax({
    //        url: '/Onboarding/RegisterUser',
    //        type: 'POST',
    //        data: { model, nameOfEnterprise, theme: (localStorage.getItem('frl-theme') || 'theme1') },
    //        success: function (response) {
    //            $btn.prop('disabled', false)
    //                .html('<i class="bi bi-send me-1"></i> Proceed & Send OTP');

    //            if (response.status) {
    //                $('#step2').addClass('d-none');
    //                $('#step3').removeClass('d-none');
    //                $('#otpEmailTarget').text(model.Email);
    //                goToRailStep(3);
    //                startOtpTimer();
    //                $('.otp-input').first().focus();
    //                showToast(response.message);
    //                $('html,body').animate({ scrollTop: 0 }, 300);
    //                if (response.otp) {
    //                    $('#otpDisplayValue').text(response.otp);
    //                    var otpModal = new bootstrap.Modal(document.getElementById('otpDisplayModal'));
    //                    otpModal.show();
    //                }
    //            }
    //            else {
    //                $('#reviewError').removeClass('d-none');
    //                $('#reviewError span').text(response.message);
    //                showToast(response.message);

    //                if (response.clearFields) {
    //                    $('#rv_email').val('');
    //                    $('#mobileInput').val('');
    //                    $('#btnProceedToOtp').prop('disabled', true);
    //                    $('#rv_email').focus();
    //                }
    //            }
    //        },
    //        error: function () {
    //            $btn.prop('disabled', false)
    //                .html('<i class="bi bi-send me-1"></i> Register');
    //            $('#reviewError').removeClass('d-none');
    //            $('#reviewError span').text('Something went wrong.');
    //        }
    //    });
    //});


    // ---------- OTP BOX BEHAVIOUR ----------
    $('.otp-input').on('input', function () {
        this.value = this.value.replace(/[^0-9]/g, '');
        if (this.value.length === 1) {
            $(this).removeClass('is-invalid');
            $('#otpError').addClass('d-none');
            $(this).next('.otp-input').focus();
        }
    }).on('keydown', function (e) {
        if (e.key === 'Backspace' && this.value === '') {
            $(this).prev('.otp-input').focus();
        }
    }).on('paste', function (e) {
        const text = (e.originalEvent.clipboardData || window.clipboardData).getData('text').replace(/[^0-9]/g, '');
        if (text.length) {
            e.preventDefault();
            const boxes = $('.otp-input');
            for (let i = 0; i < boxes.length; i++) { boxes.eq(i).val(text[i] || ''); }
            boxes.eq(Math.min(text.length, boxes.length) - 1).focus();
        }
    });



    let otpTimerInterval;

    function startOtpTimer() {
        let t = 60;
        $('#otpTimer').text('00:60');
        $('#resendOtp').addClass('disabled');
        clearInterval(otpTimerInterval);
        otpTimerInterval = setInterval(function () {
            t--;
            const s = String(t).padStart(2, '0');
            $('#otpTimer').text('00:' + s);
            if (t <= 0) {
                clearInterval(otpTimerInterval);
                $('#otpTimer').text('Expired');
                $('#resendOtp').removeClass('disabled');
            }
        }, 1000);
    }

    $('#resendOtp').on('click', function (e) {
        e.preventDefault();

        if ($(this).hasClass('disabled')) return;

        var $btn = $(this);
        var originalHtml = $btn.html();

        var email = $("#otpEmailTarget").text().trim();

        if (!email) {
            showToast('Email not found. Please restart the process.');
            return;
        }

        var model = {
            Email: email,
            theme: (localStorage.getItem('frl-theme') || 'theme1')
        };

        // Disable + show sending state while the call is in flight
        $btn.addClass('disabled');
        $btn.html('<i class="bi bi-arrow-repeat me-1"></i>Sending...');

        $.ajax({
            url: "/Auth/GenCustomerOtp",
            type: "POST",
            data: model,
            success: function (response) {
                if (response && response.status) {
                    $('.otp-input').val('');
                    $('.otp-input').first().focus();
                    showToast('A new OTP has been sent.');

                    $btn.html(originalHtml);
                    startOtpTimer();
                } else {
                    showToast('Failed to resend OTP. Try again.');
                    $btn.html(originalHtml);
                    $btn.removeClass('disabled');
                }
            },
            error: function () {
                showToast('Something went wrong while resending OTP.');
                $btn.html(originalHtml);
                $btn.removeClass('disabled');
            }
        });
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

    // ---------- STEP 3 → STEP 4 (verify OTP) ----------

    $('#btnVerifyOtp').on('click', function () {
        const code = $('.otp-input').map(function () {
            return this.value;
        }).get().join('');
        if (code.length != 6) {
            $('#otpError')
                .removeClass('d-none')
                .html('<i class="bi bi-exclamation-triangle-fill me-1"></i> Please enter all 6 digits.');
            return;
        }
        var model = {
            MsmeEnquiryID: $('#hdnMsmeEnquiryID').val(),
            // Masked — server decrypts emailEnc/mobileEnc below and uses that
            // for the actual OTP match + saved registration record.
            Email: $("#rv_email").val(),
            MobileNumber: $("#mobileInput").val(),
            OTP: code
        };
        var $btn = $(this);
        $btn.prop("disabled", true)
            .html('<span class="spin-loader"></span> Verifying...');
        $.ajax({
            url: "/Onboarding/FetchUserOTPDet",
            type: "POST",
            data: $.extend({}, model, {
                emailEnc: $('#rv_email_enc').val(),
                mobileEnc: $('#mobileInput_enc').val()
            }),
            success: function (res) {
                $btn.prop("disabled", false)
                    .html('<i class="bi bi-check-circle me-1"></i> Verify & Continue');
                if (res.status) {
                    showToast(res.message);
                    window.location.href = res.redirectUrl;
                }
                else {
                    $('#otpError')
                        .removeClass('d-none')
                        .html('<i class="bi bi-exclamation-triangle-fill me-1"></i> ' + res.message);
                    $('.otp-input').val('');
                    $('.otp-input').first().focus();
                }
            },
            error: function () {
                $btn.prop("disabled", false)
                    .html('<i class="bi bi-check-circle me-1"></i> Verify & Continue');
                alert("Something went wrong.");
            }
        });
    });


    //$('#btnVerifyOtp').on('click', function () {

    //    const code = $('.otp-input').map(function () {
    //        return this.value;
    //    }).get().join('');

    //    if (code.length != 6) {

    //        $('#otpError')
    //            .removeClass('d-none')
    //            .html('<i class="bi bi-exclamation-triangle-fill me-1"></i> Please enter all 6 digits.');

    //        return;
    //    }

    //    var model = {
    //        Email: $("#rv_email").val(),
    //        MobileNumber: $("#mobileInput").val(),
    //        OTP: code
    //    };

    //    var $btn = $(this);

    //    $btn.prop("disabled", true)
    //        .html('<span class="spin-loader"></span> Verifying...');

    //    $.ajax({

    //        url: "/Onboarding/FetchUserOTPDet",
    //        type: "POST",
    //        data: model,

    //        success: function (res) {

    //            $btn.prop("disabled", false)
    //                .html('<i class="bi bi-check-circle me-1"></i> Verify & Continue');

    //            if (res.status) {
    //                showToast(res.message);
    //                window.location.href = res.redirectUrl;
    //            }
    //            else {

    //                $('#otpError')
    //                    .removeClass('d-none')
    //                    .html('<i class="bi bi-exclamation-triangle-fill me-1"></i> ' + res.message);

    //                $('.otp-input').val('');
    //                $('.otp-input').first().focus();
    //            }
    //        },

    //        error: function () {

    //            $btn.prop("disabled", false)
    //                .html('<i class="bi bi-check-circle me-1"></i> Verify & Continue');

    //            alert("Something went wrong.");
    //        }

    //    });

    //});


});

// Email/Mobile in Contact Details are read-only, masked display fields —
// no edit-in-place affordance any more (pencil icon removed from the view).