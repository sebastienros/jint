var document={};
var navigator={};
document.createElement=function(tag){
	return new HTMLScriptElement();
}
function HTMLScriptElement(){

}