/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.3/15.4.3.2/15.4.3.2-0-7.js
 * @description Array.isArray returns false if its argument is not an Array
 */


function testcase() {
  var o = new Object();
  o[12] = 13;
  var b = Array.isArray(o);
  if (b === false) {
    return true;
  }
 }
runTestCase(testcase);
