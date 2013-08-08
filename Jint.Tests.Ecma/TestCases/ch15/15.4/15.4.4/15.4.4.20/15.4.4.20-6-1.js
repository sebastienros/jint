/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-6-1.js
 * @description Array.prototype.filter returns an empty array if 'length' is 0 (empty array)
 */


function testcase() {
  function cb(){}
  var a = [].filter(cb);
  if (Array.isArray(a) &&
      a.length === 0) {
    return true;
  }
 }
runTestCase(testcase);
