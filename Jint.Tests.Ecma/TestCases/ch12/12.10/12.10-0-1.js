/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch12/12.10/12.10-0-1.js
 * @description with does not change declaration scope - vars in with are visible outside
 */


function testcase() {
  var o = {};
  var f = function () {
	/* capture foo binding before executing with */
	return foo;
      }

  with (o) {
    var foo = "12.10-0-1";
  }

  return f()==="12.10-0-1"

 }
runTestCase(testcase);
