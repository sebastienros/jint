/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.14/15.4.4.14-4-1.js
 * @description Array.prototype.indexOf returns -1 if 'length' is 0 (empty array)
 */


function testcase() {
  var i = [].indexOf(42);
  if (i === -1) {
    return true;
  }
 }
runTestCase(testcase);
