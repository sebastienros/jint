/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-6-4.js
 * @description 'length' property of arguments object for 0 argument function call is 0 even with formal parameters
 */


function testcase() {
      var arguments= undefined;
	return (function (a,b,c) {return arguments.length === 0})();
 }
runTestCase(testcase);
