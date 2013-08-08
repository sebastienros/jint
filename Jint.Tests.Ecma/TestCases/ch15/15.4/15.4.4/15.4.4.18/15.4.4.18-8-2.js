/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-8-2.js
 * @description Array.prototype.forEach doesn't call callbackfn if 'length' is 0 (subclassed Array, length overridden to null (type conversion))
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = null;
  
  var callCnt = 0;
  function cb(){callCnt++}
  var i = f.forEach(cb);  
  if (callCnt === 0) {
    return true;
  }
 }
runTestCase(testcase);
