/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.21/15.4.4.21-10-4.js
 * @description Array.prototype.reduce - subclassed array with length more than 1
 */


function testcase() {
  foo.prototype = new Array(1, 2, 3, 4);
  function foo() {}
  var f = new foo();
  
  function cb(prevVal, curVal, idx, obj){return prevVal + curVal;}
  if(f.reduce(cb) === 10)
    return true;
 }
runTestCase(testcase);
