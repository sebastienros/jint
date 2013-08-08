/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.16/15.4.4.16-5-1.js
 * @description Array.prototype.every - thisArg not passed
 */


function testcase() {

  function callbackfn(val, idx, obj)
  {
    return this === fnGlobalObject();
  }

  var arr = [1];
  if(arr.every(callbackfn) === true)
    return true;    
 }
runTestCase(testcase);
