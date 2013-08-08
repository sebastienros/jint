/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.22/15.4.4.22-7-1.js
 * @description Array.prototype.reduceRight returns initialValue if 'length' is 0 and initialValue is present (empty array)
 */


function testcase() {
  function cb(){}
  
  try {
    if([].reduceRight(cb,1) === 1)
      return true;
  }
  catch (e) {  }  
 }
runTestCase(testcase);
