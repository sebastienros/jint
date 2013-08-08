/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.18/15.4.4.18-5-5.js
 * @description Array.prototype.forEach - thisArg is object from object template
 */


function testcase() {
  var res = false;
  var result;
  function callbackfn(val, idx, obj)
  {
    result = this.res;
  }

  function foo(){}
  var f = new foo();
  f.res = true;
  
  var arr = [1];
  arr.forEach(callbackfn,f)
  if( result === true)
    return true;    

 }
runTestCase(testcase);
