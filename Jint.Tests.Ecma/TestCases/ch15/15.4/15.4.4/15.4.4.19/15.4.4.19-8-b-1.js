/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.19/15.4.4.19-8-b-1.js
 * @description Array.prototype.map - callbackfn not called for indexes never been assigned values
 */


function testcase() { 
 
  var callCnt = 0;
  function callbackfn(val, idx, obj)
  {
    callCnt++;
    return 1;
  }

  var srcArr = new Array(10);
  srcArr[1] = undefined; //explicitly assigning a value
  var resArr = srcArr.map(callbackfn);
  if( resArr.length === 10 && callCnt === 1)
      return true;    
 }
runTestCase(testcase);
