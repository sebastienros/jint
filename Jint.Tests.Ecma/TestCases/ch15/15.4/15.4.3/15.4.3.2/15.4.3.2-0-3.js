/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-3.js
 * @description Array.isArray return true if its argument is an Array
 */


function testcase() {
  var a = [];
  var b = Array.isArray(a);
  if (b === true) {
    return true;
  }
 }
runTestCase(testcase);
