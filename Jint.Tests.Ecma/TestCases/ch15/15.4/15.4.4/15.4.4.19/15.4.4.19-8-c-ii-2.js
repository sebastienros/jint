/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-c-ii-2.js
 * @description Array.prototype.map - callbackfn takes 3 arguments
 */


function testcase() { 
 
  var parCnt = 3;
  var bCalled = false
  function callbackfn(val, idx, obj)
  { 
    bCalled = true;
    if(arguments.length !== 3)
      parCnt = arguments.length;   //verify if callbackfn was called with 3 parameters
  }

  var srcArr = [0,1,2,3,4,5,6,7,8,9];
  var resArr = srcArr.map(callbackfn);
  if(bCalled === true && parCnt === 3)
    return true;


 }
runTestCase(testcase);
