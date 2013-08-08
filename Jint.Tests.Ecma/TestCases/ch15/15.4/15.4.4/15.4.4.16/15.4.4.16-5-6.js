/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-6.js
 * @description Array.prototype.every - thisArg is function
 */


function testcase() {
  var res = false;
  function callbackfn(val, idx, obj)
  {
    return this.res;
  }

  function foo(){}
  foo.res = true;
  var arr = [1];

  if(arr.every(callbackfn,foo) === true)
    return true;    

 }
runTestCase(testcase);
