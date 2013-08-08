/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-5-6.js
 * @description Array.prototype.map - thisArg is function
 */


function testcase() {
  var res = false;
  function callbackfn(val, idx, obj)
  {
    return this.res;
  }

  function foo(){}
  foo.res = true;
  
  var srcArr = [1];
  var resArr = srcArr.map(callbackfn,foo);
  if( resArr[0] === true)
    return true;    

 }
runTestCase(testcase);
