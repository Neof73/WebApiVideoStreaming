$(document).ready(function () {

    $(':file').on('change',
        function (event, numFiles, label) {
            var sizeInKb = parseFloat($(this).prop("files")['0'].size / 1024).toFixed(2);
            var fileName = $(this).prop("files")['0'].name;
            uploadProgress = $('#dvProgress');
            $(uploadProgress).attr("aria-valuemax", sizeInKb);
            $(uploadProgress).css("width", "0%");
            $('#dvError').addClass("hidden");
            $("#btnSubir").removeAttr("disabled");

            var input = $(this).parents('.input-group').find(':text'),
                log = event.target.files[0].name;

            if (input.length) {
                input.val(log);
            }
            else {
                if (log) alert(log);
            }

        });


    $('#uploadForm').submit(function (e) {

        var formData = new FormData(this);

        $.ajax({
            type: 'POST',
            url: '/api/videos/mediaupload',
            data: formData,
            xhr: function () {
                var myXhr = $.ajaxSettings.xhr();
                if (myXhr.upload) {
                    myXhr.upload.addEventListener('progress', progress, false);
                }
                return myXhr;
            },
            cache: false,
            contentType: false,
            processData: false,

            success: function (data) {
                console.log(data);
                $('#dvProgress').addClass("progress-bar-success");
                location.reload();
            },

            error: function (jqXHR, statusText, data) {
                console.log(jqXHR);
                $('#pError').html("Se ha producido un error: " + data)
                $('#dvError').removeClass("hidden");
            }
        });

        e.preventDefault();

    });

});


function progress(e) {

    if (e.lengthComputable) {
        var max = e.total;
        var current = e.loaded;

        var Percentage = (current * 100) / max;
        console.log(Percentage);

        uploadProgress = $('#dvProgress');
        $(uploadProgress).attr("aria-valuemax", max);
        $(uploadProgress).attr("aria-valuenow", current);
        $(uploadProgress).css("width", Percentage + "%");

        if (Percentage >= 100) {
            // process completed  
        }
    }
}