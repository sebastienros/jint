/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-10.js
 * @description Array.prototype.forEach - subclassed array when length is reduced
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = 1;
  
  var callCnt = 0;
  function cb(){callCnt++}
  var i = f.forEach(cb);  
  if (callCnt === 1) {
    return true;
  }
 }
runTestCase(testcase);
