/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.2/15.2.3/15.2.3.14/15.2.3.14-3-1.js
 * @description Object.keys returns the standard built-in Array containing own enumerable properties
 */


function testcase() {
  var o = { x: 1, y: 2};

  var a = Object.keys(o);
  if (a.length === 2 &&
      a[0] === 'x' &&
      a[1] === 'y') {
    return true;
  }
 }
runTestCase(testcase);
