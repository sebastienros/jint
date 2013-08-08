/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-10-3.js
 * @description Array.prototype.filter - subclassed array when length is reduced
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3);
  function foo() {}
  var f = new foo();
  f.length = 1;
  
  function cb(){return true;}
  var a = f.filter(cb);
  
  if (Array.isArray(a) &&
      a.length === 1) {
    return true;
  }
 }
runTestCase(testcase);
