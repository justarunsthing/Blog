var index = 0;
const swalButton = Swal.mixin({
    customClass: {
        confirmButton: 'btn btn-primary'
    },
    imageUrl: '/img/post-sample-image.jpg',
    timer: 3000,
    buttonsStyling: false
});

function addTag() {
    var tagEntry = document.getElementById("tag-entry");
    var searchResult = search(tagEntry.value);

    if (searchResult != null) {
        swalButton.fire({
            html: `<span>${searchResult}</span>`
        });
    }
    else {
        // Create a new select option
        var newOption = new Option(tagEntry.value, tagEntry.value);
        document.getElementById("tags-list").options[index] = newOption;
        index++;
    }

    // Clear tag-entry control
    tagEntry.value = "";

    return true;
}

function deleteTag() {
    var tagCount = 1;
    var tagsList = document.getElementById("tags-list");

    if (!tagsList) {
        return false;
    }

    if (tagsList.selectedIndex == -1) {
        swalButton.fire({
            html: "<span class='font-weight-bolder'>Please select a tag to delete</span>"
        });

        return true;
    }

    while (tagCount > 0) {
        if (tagsList.selectedIndex >= 0) {
            tagsList.options[tagsList.selectedIndex] = null;
            --tagCount;
        }
        else {
            tagCount = 0;
            index--;
        }
    }
}

$("form").on("submit", function () {
    $("#tags-list option").prop("selected", "selected");
});

// Populate any existing tags
if (tagValues != '') {
    var tagArray = tagValues.split(",");

    for (var i = 0; i < tagArray.length; i++) {
        replaceTag(tagArray[i], i);
        index++;
    }
}

function replaceTag(tag, index) {
    var newOption = new Option(tag, tag);

    document.getElementById("tags-list").options[index] = newOption;
}

// Detects empty or duplicate tags & returns errors
function search(str) {
    if (str == "") {
        return "Empty tags are not permitted";
    }

    var tagsElement = document.getElementById("tags-list");

    if (tagsElement) {
        var options = tagsElement.options;

        for (var i = 0; i < options.length; i++) {
            if (options[i].value == str) {
                return `Duplicate tag #${str} detected`;
            }
        }
    }
}