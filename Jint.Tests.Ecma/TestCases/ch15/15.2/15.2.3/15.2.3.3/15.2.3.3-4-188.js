/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.3/15.2.3.3-4-188.js
 * @description Object.getOwnPropertyDescriptor returns undefined for non-existent properties on built-ins (Function (instance).name)
 */


function testcase() {
  var f = Function('return 42;');
  var desc = Object.getOwnPropertyDescriptor(f, "functionNameHopefullyDoesNotExist");
  return desc === undefined;
 }
runTestCase(testcase);
