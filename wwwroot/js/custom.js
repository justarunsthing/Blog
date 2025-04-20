var index = 0;

function addTag() {
    var tagEntry = document.getElementById("tag-entry");

    // Create a new select option
    var newOption = new Option(tagEntry.value, tagEntry.value);
    document.getElementById("tags-list").options[index++] = newOption;

    // Clear tag-entry control
    tagEntry.value = "";

    return true;
}

function deleteTag() {
    var tagCount = 1;

    while (tagCount > 0) {
        var tagslist = document.getElementById("tags-list");
        var selectedIndex = tagslist.selectedIndex;

        if (selectedIndex >= 0) {
            tagslist.options[selectedIndex] = null;
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