/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-8-1.js
 * @description Array.prototype.every returns true if 'length' is 0 (empty array)
 */


function testcase() {
  function cb() {}
  var i = [].every(cb);
  if (i === true) {
    return true;
  }
 }
runTestCase(testcase);
