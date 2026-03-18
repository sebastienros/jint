/*global Handlebars */
var renderTemplate = (function (handlebars, undefined) {
	'use strict';

	var compilationOptions = {
		"knownHelpers": {
			"link": true
		},
		"knownHelpersOnly": true
	};

	handlebars.registerHelper('link', function (text, url, newWindow) {
		var escapedText = handlebars.Utils.escapeExpression(text),
			escapedUrl = handlebars.Utils.escapeExpression(url)
			;

		return new handlebars.SafeString(
			'<a href="' + escapedUrl + '"' + (newWindow ? ' target="_blank"' : '') + '>' + escapedText + '</a>'
		);
	});

	/**
	* Renders a Handlebars templates
	*
	* @param {String} sourceCode - Source code of Handlebars template
	* @param {String} serializedData - A string containing JSON data
	* @returns {String} HTML code
	* @expose
	*/
	function renderTemplate(sourceCode, serializedData) {
		var data,
			template,
			content
			;

		template = handlebars.compile(sourceCode, compilationOptions);
		data = JSON.parse(serializedData);
		content = template(data);

		return content;
	}

	return renderTemplate;
}(Handlebars));