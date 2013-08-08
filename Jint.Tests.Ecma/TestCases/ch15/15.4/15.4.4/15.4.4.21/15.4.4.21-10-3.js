/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-3.js
 * @description Array.prototype.reduce - subclassed array of length 1
 */


function testcase() {
  foo.prototype = [1];
  function foo() {}
  var f = new foo();
  
  function cb(){}
  if(f.reduce(cb) === 1)
    return true;
 }
runTestCase(testcase);
