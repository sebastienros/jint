/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.20/15.4.4.20-5-4.js
 * @description Array.prototype.filter - thisArg is object from object template(prototype)
 */


function testcase() {
  var res = false;
  function callbackfn(val, idx, obj)
  {
    return this.res;
  }
  
  function foo(){}
  foo.prototype.res = true;
  var f = new foo();

  var srcArr = [1];
  var resArr = srcArr.filter(callbackfn,f);
  if( resArr.length === 1)
    return true;    

 }
runTestCase(testcase);
