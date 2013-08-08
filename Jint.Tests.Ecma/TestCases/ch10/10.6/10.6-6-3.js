/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch10/10.6/10.6-6-3.js
 * @description 'length' property of arguments object for 0 argument function exists
 */


function testcase() {
      var arguments= undefined;
	return (function () {return arguments.length !== undefined})();
 }
runTestCase(testcase);
