/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch11/11.4/11.4.1/11.4.1-2-1.js
 * @description delete operator returns true when deleting a non-reference (number)
 */


function testcase() {
  var d = delete 42;
  if (d === true) {
    return true;
  }
 }
runTestCase(testcase);
