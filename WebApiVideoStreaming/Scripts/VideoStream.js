$(document).ready(function () {
    $(':file').on('change',
        function (event, numFiles, label) {

            var input = $(this).parents('.input-group').find(':text'),
                log = event.target.files[0].name;

            if (input.length) {
                input.val(log);
            }
            else {
                if (log) alert(log);
            }

        });
});