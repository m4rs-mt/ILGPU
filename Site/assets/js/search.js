---
# Add this header to tell Jekyll to process the file
---

{% if site.search.enabled %}
/***** Lunr Index *****/
const searchDocuments = [
    {% for doc in site.docs %}
        {% if doc.path != site.data.sidebar.main.path %}
        {
            "title": "{{ doc.title }}",
            "content": `{{ doc.content | markdownify | strip_html }}`,
            "url": "{{ doc.url | relative_url }}"
        }
        {% unless forloop.last %},{% endunless %}
        {% endif %}
    {% endfor %}
];


const searchIndex = lunr(function () {
    this.ref('url')
    this.field('title');
    this.field('content');
    searchDocuments.forEach(function (doc) {
        this.add(doc);
    }, this);
})
/***** Lunr Index *****/


/***** TypeAhead *****/
function findDocumentByFieldValue(field, value) {
    const results = searchDocuments.find(obj => {
        return obj[field] === value
    });
    if (results.length === 0) {
        return undefined;
    }else if (results.length === 1) {
        return results[0];
    }else {
        return results;
    }
}


function findMatches(query, callback) {
    const matches = searchIndex.search(query).map(({ ref }) => {
        return findDocumentByFieldValue('url', ref);
    });
    callback(matches);
}

// https://github.com/twitter/typeahead.js/blob/master/doc/jquery_typeahead.md
$('#search .typeahead').typeahead({
        // hint: true,
        // highlight: true,
        minLength: 1,
        classNames: {
            menu: 'bg-white',
            cursor: 'text-primary',
        }
    },
    {
        name: 'documentation',
        source: findMatches,
        display: 'title',
        limit: 3,
        templates: {
            empty: `<div class="empty-message">
                No matches found
                </div>`,
            suggestion: (s) => (`<div role="button">${s.title}</div>`),
        }
    });


$('.typeahead').bind('typeahead:select', function(ev, suggestion) {
    if (suggestion?.url) {
        window.location.href = suggestion.url;
    }
});
/***** TypeAhead *****/
{% endif %}
