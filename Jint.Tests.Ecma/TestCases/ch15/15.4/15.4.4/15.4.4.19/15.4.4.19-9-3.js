/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-9-3.js
 * @description Array.prototype.map - subclassed array when length is reduced
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = 1;
  
  function cb(){}
  var a = f.map(cb);
  
  if (Array.isArray(a) &&
      a.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
