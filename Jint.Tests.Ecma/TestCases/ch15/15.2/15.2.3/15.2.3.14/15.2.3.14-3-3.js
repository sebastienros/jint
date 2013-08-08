/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-3.js
 * @description Object.keys returns the standard built-in Array containing own enumerable properties (array)
 */


function testcase() {
  var o = [1, 2];
  var a = Object.keys(o);
  if (a.length === 2 &&
      a[0] === '0' &&
      a[1] === '1') {
    return true;
  }
 }
runTestCase(testcase);
