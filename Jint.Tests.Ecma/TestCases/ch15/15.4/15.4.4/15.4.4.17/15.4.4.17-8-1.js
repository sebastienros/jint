/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.17/15.4.4.17-8-1.js
 * @description Array.prototype.some returns false if 'length' is 0 (empty array)
 */


function testcase() {
  function cb(){}
  var i = [].some(cb);
  if (i === false) {
    return true;
  }
 }
runTestCase(testcase);
