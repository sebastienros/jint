/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-5-4.js
 * @description Array.prototype.indexOf returns 0 if fromIndex is 'undefined'
 */


function testcase() {
  var a = [1,2,3];
  if (a.indexOf(1,undefined) === 0) {    // undefined resolves to 0
    return true;
  }
 }
runTestCase(testcase);
