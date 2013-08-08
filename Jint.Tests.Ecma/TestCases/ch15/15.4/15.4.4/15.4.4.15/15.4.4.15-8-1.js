/// Copyright (c) 2012 Ecma International.  All rights reserved. 
/**
 * @path ch15/15.4/15.4.4/15.4.4.15/15.4.4.15-8-1.js
 * @description Array.prototype.lastIndexOf must return correct index(boolean)
 */


function testcase() {
  var obj = {toString:function (){return true}};
  var _false = false;
  var a = new Array(false,true,false,obj,_false,true,"true", undefined,0,null,1,"str",0,1);
  if (a.lastIndexOf(true) === 5 &&  //a[5]=true
      a.lastIndexOf(false) === 4)    //a[4] =_false
  {
    return true;
  }
 }
runTestCase(testcase);
