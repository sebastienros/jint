/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-10-8.js
 * @description Array.prototype.reduceRight doesn't visit expandos
 */


function testcase() {

  var callCnt = 0;
  function callbackfn(prevVal, curVal,  idx, obj)
  {
    callCnt++;
  }
  var srcArr = ['1','2','3','4','5'];
  srcArr["i"] = 10;
  srcArr[true] = 11;

  srcArr.reduceRight(callbackfn);

  if(callCnt == 4)
  {
    return true;
  }

 }
runTestCase(testcase);
