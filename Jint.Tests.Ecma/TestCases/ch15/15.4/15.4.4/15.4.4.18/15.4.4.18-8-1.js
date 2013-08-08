/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-1.js
 * @description Array.prototype.forEach doesn't call callbackfn if 'length' is 0 (empty array)
 */


function testcase() {
  var callCnt = 0;
  function cb(){callCnt++}
  var i = [].forEach(cb);
  if (callCnt === 0) {
    return true;
  }
 }
runTestCase(testcase);
