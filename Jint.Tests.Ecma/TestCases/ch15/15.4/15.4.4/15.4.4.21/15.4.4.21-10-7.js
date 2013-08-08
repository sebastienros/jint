/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-7.js
 * @description Array.prototype.reduce - subclassed array with length 1 and initialvalue provided
 */


function testcase() {
  foo.prototype = [1];
  function foo() {}
  var f = new foo();
  
  function cb(prevVal, curVal, idx, obj){return prevVal + curVal;}
  if(f.reduce(cb,-1) === 0)
    return true;
 }
runTestCase(testcase);
